﻿// Copyright (c) Allan Hardy. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Linq;
using App.Metrics.Abstractions.Filtering;
using App.Metrics.Abstractions.MetricTypes;
using App.Metrics.Abstractions.ReservoirSampling;
using App.Metrics.Core.Options;
using App.Metrics.Facts.Fixtures;
using App.Metrics.Filtering;
using App.Metrics.ReservoirSampling.ExponentialDecay;
using App.Metrics.ReservoirSampling.Uniform;
using App.Metrics.Tagging;
using App.Metrics.Timer.Abstractions;
using FluentAssertions;
using Moq;
using Xunit;

namespace App.Metrics.Facts.Providers
{
    public class DefaultTimerMetricProviderTests : IClassFixture<MetricCoreTestFixture>
    {
        private readonly IFilterMetrics _filter = new DefaultMetricsFilter().WhereType(MetricType.Timer);
        private readonly MetricCoreTestFixture _fixture;
        private readonly IProvideTimerMetrics _provider;


        public DefaultTimerMetricProviderTests(MetricCoreTestFixture fixture)
        {
            _fixture = fixture;
            _provider = _fixture.Providers.Timer;
        }

        [Fact]
        public void can_add_add_new_instance_to_registry()
        {
            var metricName = "timer_provider_metric_test";
            var options = new TimerOptions
                          {
                              Name = metricName
                          };

            var timerMetric = _fixture.Builder.Timer.Build(() => new DefaultAlgorithmRReservoir(1028), _fixture.Clock);

            _provider.Instance(options, () => timerMetric);

            _filter.WhereMetricName(name => name == metricName);

            _fixture.Registry.GetData(_filter).Contexts.First().Timers.Count().Should().Be(1);
        }

        [Fact]
        public void can_add_add_new_multidimensional_to_registry()
        {
            var metricName = "timer_provider_metric_test_multi";
            var options = new TimerOptions
                          {
                              Name = metricName
                          };

            var timerMetric = _fixture.Builder.Timer.Build(() => new DefaultAlgorithmRReservoir(1028), _fixture.Clock);

            _provider.Instance(options, _fixture.Tags[0], () => timerMetric);

            _filter.WhereMetricName(name => name == _fixture.Tags[0].AsMetricName(metricName));

            _fixture.Registry.GetData(_filter).Contexts.First().Timers.Count().Should().Be(1);
        }

        [Fact]
        public void can_add_instance_to_registry()
        {
            var metricName = "timer_provider_test";
            var options = new TimerOptions
                          {
                              Name = metricName
                          };

            _provider.Instance(options);

            _filter.WhereMetricName(name => name == metricName);

            _fixture.Registry.GetData(_filter).Contexts.First().Timers.Count().Should().Be(1);
        }

        [Fact]
        public void can_add_instance_with_histogram()
        {
            var reservoirMock = new Mock<IHistogramMetric>();
            reservoirMock.Setup(r => r.Update(It.IsAny<long>(), null));
            reservoirMock.Setup(r => r.Reset());

            var options = new TimerOptions
                          {
                              Name = "timer_custom_histogram"
                          };

            var timer = _provider.WithHistogram(options, () => reservoirMock.Object);

            using (timer.NewContext())
            {
                _fixture.Clock.Advance(TimeUnit.Milliseconds, 100);
            }

            reservoirMock.Verify(r => r.Update(It.IsAny<long>(), null), Times.Once);
        }

        [Fact]
        public void can_add_multidimensional_to_registry()
        {
            var metricName = "timer_provider_test_multi";
            var options = new TimerOptions
                          {
                              Name = metricName
                          };

            _provider.Instance(options, _fixture.Tags[0]);

            _filter.WhereMetricName(name => name == _fixture.Tags[0].AsMetricName(metricName));

            _fixture.Registry.GetData(_filter).Contexts.First().Timers.Count().Should().Be(1);
        }

        [Fact]
        public void can_add_multidimensional_with_histogram()
        {
            var reservoirMock = new Mock<IHistogramMetric>();
            reservoirMock.Setup(r => r.Update(It.IsAny<long>(), null));
            reservoirMock.Setup(r => r.Reset());

            var options = new TimerOptions
                          {
                              Name = "timer_custom_histogram_multi"
                          };

            var timer = _provider.WithHistogram(options, _fixture.Tags[0], () => reservoirMock.Object);

            using (timer.NewContext())
            {
                _fixture.Clock.Advance(TimeUnit.Milliseconds, 100);
            }

            reservoirMock.Verify(r => r.Update(It.IsAny<long>(), null), Times.Once);
        }

        [Fact]
        public void can_use_custom_reservoir()
        {
            var reservoirMock = new Mock<IReservoir>();
            reservoirMock.Setup(r => r.Update(It.IsAny<long>(), null));
            reservoirMock.Setup(r => r.GetSnapshot()).Returns(() => new UniformSnapshot(100L, 100.0, new long[100]));
            reservoirMock.Setup(r => r.Reset());

            IReservoir Reservoir() => reservoirMock.Object;

            var options = new TimerOptions
                          {
                              Name = "timer_provider_custom_test",
                              Reservoir = Reservoir
            };

            var timer = _provider.Instance(options);

            using (timer.NewContext())
            {
                _fixture.Clock.Advance(TimeUnit.Milliseconds, 100);
            }

            reservoirMock.Verify(r => r.Update(It.IsAny<long>(), null), Times.Once);
        }

        [Fact]
        public void can_use_custom_reservoir_when_multidimensional()
        {
            var reservoirMock = new Mock<IReservoir>();
            reservoirMock.Setup(r => r.Update(It.IsAny<long>(), null));
            reservoirMock.Setup(r => r.GetSnapshot()).Returns(() => new UniformSnapshot(100L, 100.0, new long[100]));
            reservoirMock.Setup(r => r.Reset());

            IReservoir Reservoir() => reservoirMock.Object;

            var options = new TimerOptions
                          {
                              Name = "timer_provider_custom_test_multi",
                              Reservoir = Reservoir
                          };

            var timer = _provider.Instance(options, _fixture.Tags[0]);

            using (timer.NewContext())
            {
                _fixture.Clock.Advance(TimeUnit.Milliseconds, 100);
            }

            reservoirMock.Verify(r => r.Update(It.IsAny<long>(), null), Times.Once);
        }

        [Fact]
        public void multidimensional_should_not_share_reservoir_when_changed_from_default()
        {
            var timerDef = new TimerOptions
                           {
                               Reservoir = () => new DefaultForwardDecayingReservoir()
                           };

            using (var metricsFixture = new MetricsFixture())
            {
                var timer1 = metricsFixture.Metrics.Provider.Timer.Instance(timerDef, new MetricTags("test", "1"));
                var timer2 = metricsFixture.Metrics.Provider.Timer.Instance(timerDef, new MetricTags("test", "2"));

                timer1.Record(100, TimeUnit.Seconds);
                timer1.Record(100, TimeUnit.Seconds);
                timer1.Record(100, TimeUnit.Seconds);
                timer1.Record(100, TimeUnit.Seconds);
                timer1.Record(100, TimeUnit.Seconds);

                var timers = metricsFixture.Metrics.Snapshot.Get().Contexts.First().Timers.ToArray();

                Assert.Equal(5, timers[0].Value.Histogram.Count);
                Assert.Equal(100_000, timers[0].Value.Histogram.Mean);

                Assert.Equal(0, timers[1].Value.Histogram.Mean);
                Assert.Equal(0, timers[1].Value.Histogram.Count);
            }
        }        
    }
}