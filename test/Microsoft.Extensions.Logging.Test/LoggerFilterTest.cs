﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Microsoft.Extensions.Logging.Test
{
    public class LoggerFilterTest
    {
        [Fact]
        public void ChangingConfigReloadsDefaultFilter()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Microsoft"": ""Information""
    }
  }
}";
            var config = CreateConfiguration(() => json);
            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);

            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider));

            var logger = factory.CreateLogger("Microsoft");

            // Act
            logger.LogTrace("Message");

            // Assert
            var writes = loggerProvider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            json =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Microsoft"": ""Trace""
    }
  }
}";
            config.Reload();

            // Act
            logger.LogTrace("Message");

            // Assert
            writes = loggerProvider.Sink.Writes;
            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void ChangingConfigFromUseConfigurationReloadsDefaultFilter()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Microsoft"": ""Information""
    }
  }
}";
            var config = CreateConfiguration(() => json);
            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider));

            var logger = factory.CreateLogger("Microsoft");

            // Act
            logger.LogTrace("Message");

            // Assert
            var writes = loggerProvider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            json =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Microsoft"": ""Trace""
    }
  }
}";
            config.Reload();

            // Act
            logger.LogTrace("Message");

            // Assert
            writes = loggerProvider.Sink.Writes;
            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void CanFilterOnNamedProviders()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""Microsoft.Extensions.Logging.Test.TestLoggerProvider"": {
      ""LogLevel"": {
        ""Microsoft"": ""Information""
      }
    }
  }
}";
            var config = CreateConfiguration(() => json);

            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider));

            var logger = factory.CreateLogger("Microsoft");

            // Act
            logger.LogTrace("Message");

            // Assert
            var writes = loggerProvider.Sink.Writes;
            Assert.Equal(0, writes.Count);
        }

        [Fact]
        public void PreferFullNameOverDefaultForFiltering()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Microsoft"": ""Critical""
    },
    ""Microsoft.Extensions.Logging.Test.TestLoggerProvider"": {
      ""LogLevel"": {
        ""Microsoft"": ""Trace""
      }
    }
  }
}";
            var config = CreateConfiguration(() => json);

            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider));

            var logger = factory.CreateLogger("Microsoft");

            // Act
            logger.LogTrace("Message");

            // Assert
            var writes = loggerProvider.Sink.Writes;
            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void DefaultCategoryNameIsUsedIfNoneMatch()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""Microsoft.Extensions.Logging.Test.TestLoggerProvider"": {
      ""LogLevel"": {
        ""Default"": ""Information"",
        ""Microsoft"": ""Warning""
      }
    }
  }
}";
            var config = CreateConfiguration(() => json);

            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider));

            var logger = factory.CreateLogger("Microsoft");

            // Act
            logger.LogTrace("Message");

            // Assert
            var writes = loggerProvider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            // No config value for 'None' so should use 'Default'
            logger = factory.CreateLogger("None");

            // Act
            logger.LogTrace("Message");

            // Assert
            Assert.Equal(0, writes.Count);

            // Act
            logger.LogInformation("Message");

            // Assert
            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void AddFilterForMatchingProviderFilters()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter((name, cat, level) =>
                {
                    if (string.Equals("Microsoft.Extensions.Logging.Test.TestLoggerProvider", name))
                    {
                        if (string.Equals("Test", cat))
                        {
                            return level >= LogLevel.Information;
                        }
                    }

                    return true;
                }));

            var logger = factory.CreateLogger("Test");

            logger.LogInformation("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(1, writes.Count);

            logger.LogTrace("Message");

            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void AddFilterForNonMatchingProviderDoesNotFilter()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter((name, cat, level) =>
                {
                    if (string.Equals("None", name))
                    {
                        return level >= LogLevel.Error;
                    }

                    return true;
                }));

            var logger = factory.CreateLogger("Test");

            logger.LogInformation("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void AddFilterLastWins()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter((name, cat, level) => level >= LogLevel.Warning)
                .AddFilter((name, cat, level) => string.Equals(cat, "NotTest")));

            var logger = factory.CreateLogger("Test");

            logger.LogWarning("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            logger = factory.CreateLogger("NotTest");

            logger.LogInformation("Message");

            Assert.Equal(1, writes.Count);

            logger.LogError("Message");

            Assert.Equal(2, writes.Count);
        }

        [Fact]
        public void ProviderLevelIsPreferredOverGlobalFilter()
        {
            // Arrange
            var json =
@"{
  ""Logging"": {
    ""Microsoft.Extensions.Logging.Test.TestLoggerProvider"": {
      ""LogLevel"": {
        ""Test"": ""Debug""
      }
    }
  }
}";
            var config = CreateConfiguration(() => json);
            var loggerProvider = new TestLoggerProvider(new TestSink(), isEnabled: true);

            var factory = TestLoggerBuilder.Create(builder => builder
                .AddConfiguration(config.GetSection("Logging"))
                .AddProvider(loggerProvider)
                .AddFilter((name, cat, level) => level < LogLevel.Critical));

            var logger = factory.CreateLogger("Test");

            var writes = loggerProvider.Sink.Writes;

            logger.LogTrace("Message");

            Assert.Equal(0, writes.Count);

            logger.LogInformation("Message");

            Assert.Equal(1, writes.Count);

            logger.LogCritical("Message");

            Assert.Equal(2, writes.Count);
        }

        [Fact]
        public void AddFilterWithProviderNameCategoryNameAndFilterFuncFiltersCorrectly()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter<TestLoggerProvider>((name, cat, level) => level >= LogLevel.Warning));

            var logger = factory.CreateLogger("Sample.Test");

            logger.LogInformation("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            logger.LogWarning("Message");

            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void AddFilterWithProviderNameCategoryNameAndMinLevelFiltersCorrectly()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter<TestLoggerProvider>("Sample", LogLevel.Warning));

            var logger = factory.CreateLogger("Sample.Test");

            logger.LogInformation("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            logger.LogWarning("Message");

            Assert.Equal(1, writes.Count);
        }

        [Fact]
        public void AddFilterWithProviderNameAndCategoryFilterFuncFiltersCorrectly()
        {
            var provider = new TestLoggerProvider(new TestSink(), isEnabled: true);
            var factory = TestLoggerBuilder.Create(builder => builder
                .AddProvider(provider)
                .AddFilter<TestLoggerProvider>((c, l) => l >= LogLevel.Warning));

            var logger = factory.CreateLogger("Sample.Test");

            logger.LogInformation("Message");

            var writes = provider.Sink.Writes;
            Assert.Equal(0, writes.Count);

            logger.LogWarning("Message");

            Assert.Equal(1, writes.Count);
        }

        internal ConfigurationRoot CreateConfiguration(Func<string> getJson)
        {
            var provider = new TestConfiguration(new JsonConfigurationSource { Optional = true }, getJson);
            return new ConfigurationRoot(new List<IConfigurationProvider> { provider });
        }

        private class TestConfiguration : JsonConfigurationProvider
        {
            private Func<string> _json;
            public TestConfiguration(JsonConfigurationSource source, Func<string> json)
                : base(source)
            {
                _json = json;
            }

            public override void Load()
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.Write(_json());
                writer.Flush();
                stream.Seek(0, SeekOrigin.Begin);
                Load(stream);
            }
        }
    }
}
