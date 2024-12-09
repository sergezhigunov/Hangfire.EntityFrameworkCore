using System.Globalization;
using Hangfire.Common;
using Hangfire.States;

namespace Hangfire.EntityFrameworkCore.Tests;

public class EFCoreStorageMonitoringApiFacts : EFCoreStorageTest
{
    [Fact]
    public static void Ctor_Throws_WhenStorageParameterIsNull()
    {
        EFCoreStorage storage = null;

        Assert.Throws<ArgumentNullException>(nameof(storage),
            () => new EFCoreStorageMonitoringApi(storage));
    }

    [Fact]
    public void Ctor_CreatesInstance()
    {
        var storage = CreateStorageStub();

        var instance = new EFCoreStorageMonitoringApi(storage);

        Assert.Same(storage, Assert.IsType<EFCoreStorage>(instance.GetFieldValue("_storage")));
    }

    [Fact]
    public void DeletedJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var data = new Dictionary<string, string>
        {
            ["DeletedAt"] = JobHelper.SerializeDateTime(now),
        };
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 5).
            Select(x => new HangfireJob
            {
                CreatedAt = now + new TimeSpan(0, 0, x),
                InvocationData = invocationData,
                States =
                [
                    new()
                    {
                        CreatedAt = DateTime.UtcNow,
                        Name = DeletedState.StateName,
                        Data = data,
                    },
                ],
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.DeletedJobs(1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(now, value.DeletedAt);
        });

        Assert.Equal(jobs[2].Id.ToString(CultureInfo.InvariantCulture), result[1].Key);
        Assert.Equal(jobs[3].Id.ToString(CultureInfo.InvariantCulture), result[0].Key);
    }

    [Fact]
    public void DeletedListCount_ReturnsCorrectResult()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, DeletedState.StateName);
        });

        var result = UseMonitoringApi(instance => instance.DeletedListCount());

        Assert.Equal(3, result);
    }

    [Fact]
    public void EnqueuedCount_Throws_WhenQueueParameterIsNull()
    {
        string queue = null;

        UseMonitoringApi(instance => Assert.Throws<ArgumentNullException>(nameof(queue),
            () => instance.EnqueuedCount(queue)));
    }

    [Fact]
    public void EnqueuedCount_ReturnsCorrectResult()
    {
        string queue = "queue";
        var now = DateTime.UtcNow;
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 3).
            Select(x => new HangfireJob
            {
                CreatedAt = now + new TimeSpan(0, 0, x),
                InvocationData = invocationData,
                QueuedJobs =
                [
                    new()
                    {
                        Queue = queue,
                    },
                ],
            }).
            ToArray();
        UseContextSavingChanges(context => context.AddRange(jobs));

        var result = UseMonitoringApi(instance => instance.EnqueuedCount(queue));

        Assert.Equal(3, result);
    }

    [Fact]
    public void EnqueuedJobs_Throws_WhenQueueParameterIsNull()
    {
        string queue = null;
        int from = 0;
        int perPage = 1;

        UseMonitoringApi(instance => Assert.Throws<ArgumentNullException>(nameof(queue),
            () => instance.EnqueuedJobs(queue, from, perPage)));
    }

    [Fact]
    public void EnqueuedJobs_ReturnsEmptyResult_WhenQueueIsEmpty()
    {
        var queue = "queue";

        var result = UseMonitoringApi(instance => instance.EnqueuedJobs(queue, 0, 50));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void EnqueuedJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var queue = "queue"; ;
        var invocationData = CreateInvocationData(() => SampleMethod("Argument"));
        var jobs = Enumerable.Range(0, 3).
            Select(x =>
            {
                var createdAt = now - new TimeSpan(0, 0, x);
                var data = new Dictionary<string, string>
                {
                    ["EnqueuedAt"] = JobHelper.SerializeDateTime(createdAt),
                };
                var state = new HangfireState
                {
                    CreatedAt = createdAt,
                    Name = EnqueuedState.StateName,
                    Reason = "Reason",
                    Data = data,
                };
                var job = new HangfireJob
                {
                    CreatedAt = createdAt,
                    InvocationData = invocationData,
                    States =
                    [
                        state,
                    ],
                    QueuedJobs =
                    [
                        new()
                        {
                            Queue = queue,
                        },
                    ]
                };
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.EnqueuedJobs(queue, 1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var id = long.Parse(item.Key, CultureInfo.InvariantCulture);
            var job = Assert.Single(jobs, x => x.Id == id);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(job.State.CreatedAt, value.EnqueuedAt);
            Assert.True(value.InEnqueuedState);
            Assert.Equal(job.StateName, value.State);
            Assert.Equal(job.State.Name, value.State);
        });
    }

    [Fact]
    public void FailedByDatesCount_ReturnsCorrectResult()
    {
        var today = DateTime.UtcNow.Date;
        var counts = Enumerable.Range(0, 7);
        var dictionaryDates = counts.ToDictionary(x => today.AddDays(-x));

        UseContextSavingChanges(context => context.AddRange(
            dictionaryDates.Select(item => new HangfireCounter
            {
                Key = $"stats:failed:{item.Key:yyyy-MM-dd}",
                Value = item.Value,
            })));

        var result = UseMonitoringApi(instance => instance.FailedByDatesCount());

        Assert.NotNull(result);
        Assert.Equal(7, result.Count);
        Assert.All(result, item => Assert.Equal(dictionaryDates[item.Key], item.Value));
    }

    [Fact]
    public void FailedCount_ReturnsCorrectResult()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, FailedState.StateName);
        });

        var result = UseMonitoringApi(instance => instance.FailedCount());

        Assert.Equal(3, result);
    }

    [Fact]
    public void FailedJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var data = new Dictionary<string, string>
        {
            ["FailedAt"] = JobHelper.SerializeDateTime(now),
            ["ExceptionDetails"] = "ExceptionDetails",
            ["ExceptionMessage"] = "ExceptionMessage",
            ["ExceptionType"] = "ExceptionType",
        };
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 5).
            Select(x =>
            {
                var job = new HangfireJob
                {
                    CreatedAt = now + new TimeSpan(0, 0, x),
                    InvocationData = invocationData,
                };
                var state = new HangfireState
                {
                    CreatedAt = DateTime.UtcNow,
                    Name = FailedState.StateName,
                    Data = data,
                    Reason = "Reason",
                };
                job.States.Add(state);
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.FailedJobs(1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(now, value.FailedAt);
            Assert.Equal("ExceptionDetails", value.ExceptionDetails);
            Assert.Equal("ExceptionMessage", value.ExceptionMessage);
            Assert.Equal("ExceptionType", value.ExceptionType);
            Assert.Equal("Reason", value.Reason);
        });
        Assert.Equal(jobs[2].Id.ToString(CultureInfo.InvariantCulture), result[1].Key);
        Assert.Equal(jobs[3].Id.ToString(CultureInfo.InvariantCulture), result[0].Key);
    }

    [Fact]
    public void FetchedCount_Throws_WhenQueueParameterIsNull()
    {
        string queue = null;

        UseMonitoringApi(instance => Assert.Throws<ArgumentNullException>(nameof(queue),
            () => instance.FetchedCount(queue)));
    }

    [Fact]
    public void FetchedCount_ReturnsCorrectResult()
    {
        string queue = "queue";
        var now = DateTime.UtcNow;
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 3).
            Select(x => new HangfireJob
            {
                CreatedAt = now + new TimeSpan(0, 0, x),
                InvocationData = invocationData,
                QueuedJobs =
                [
                    new()
                    {
                        Queue = queue,
                        FetchedAt = now,
                    },
                ],
            }).
            ToArray();
        UseContextSavingChanges(context => context.AddRange(jobs));

        var result = UseMonitoringApi(instance => instance.FetchedCount(queue));

        Assert.Equal(3, result);
    }

    [Fact]
    public void FetchedJobs_Throws_WhenQueueParameterIsNull()
    {
        string queue = null;
        int from = 0;
        int perPage = 1;

        UseMonitoringApi(instance => Assert.Throws<ArgumentNullException>(nameof(queue),
            () => instance.FetchedJobs(queue, from, perPage)));
    }

    [Fact]
    public void FetchedJobs_ReturnsEmptyResult_WhenQueueIsEmpty()
    {
        var queue = "queue";

        var result = UseMonitoringApi(instance => instance.FetchedJobs(queue, 0, 50));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void FetchedJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var queue = "queue"; ;
        var invocationData = CreateInvocationData(() => SampleMethod("Argument"));
        var jobs = Enumerable.Range(0, 3).
            Select(x =>
            {
                var createdAt = now - new TimeSpan(0, 0, x);
                var data = new Dictionary<string, string>
                {
                    ["EnqueuedAt"] = JobHelper.SerializeDateTime(createdAt),
                };
                var state = new HangfireState
                {
                    CreatedAt = createdAt,
                    Name = EnqueuedState.StateName,
                    Reason = "Reason",
                    Data = data,
                };
                var job = new HangfireJob
                {
                    CreatedAt = createdAt,
                    InvocationData = invocationData,
                    States =
                    [
                        state,
                    ],
                    QueuedJobs =
                    [
                        new()
                        {
                            Queue = queue,
                            FetchedAt = now,
                        },
                    ]
                };
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.FetchedJobs(queue, 1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var id = long.Parse(item.Key, CultureInfo.InvariantCulture);
            var job = Assert.Single(jobs, x => x.Id == id);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(now, value.FetchedAt);
            Assert.Equal(job.StateName, value.State);
            Assert.Equal(job.State.Name, value.State);
        });
    }

    [Fact]
    public void GetStatistics_ReturnsZeroes_WhenDatabaseClean()
    {
        var result = UseMonitoringApi(instance => instance.GetStatistics());

        Assert.NotNull(result);
        Assert.Equal(0, result.Deleted);
        Assert.Equal(0, result.Enqueued);
        Assert.Equal(0, result.Failed);
        Assert.Equal(0, result.Processing);
        Assert.Equal(0, result.Queues);
        Assert.Equal(0, result.Recurring);
        Assert.Equal(0, result.Scheduled);
        Assert.Equal(0, result.Servers);
        Assert.Equal(0, result.Succeeded);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        var startedAt = DateTime.UtcNow;

        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 1; i++)
                AddJobWithStateToContext(context, EnqueuedState.StateName);

            for (int i = 0; i < 2; i++)
                AddJobWithStateToContext(context, FailedState.StateName);

            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, ProcessingState.StateName);

            for (int i = 0; i < 4; i++)
                AddJobWithStateToContext(context, ScheduledState.StateName);

            context.AddRange(
                new HangfireCounter
                {
                    Key = "stats:deleted",
                    Value = 5,
                },
                new HangfireCounter
                {
                    Key = "stats:succeeded",
                    Value = 6,
                });

            for (int i = 0; i < 7; i++)
                context.Add(new HangfireSet
                {
                    Key = "recurring-jobs",
                    Value = $"recurring-job-{i}",
                });

            for (int i = 0; i < 8; i++)
                context.Add(new HangfireServer
                {
                    Id = $"server-id-{i}",
                    StartedAt = startedAt,
                    Heartbeat = startedAt,
                    Queues = EmptyArrayStub,
                });

            for (int i = 0; i < 9; i++)
                AddJobWithQueueItemToContext(context, Guid.NewGuid().ToString());
        });

        var result = UseMonitoringApi(instance => instance.GetStatistics());

        Assert.NotNull(result);
        Assert.Equal(5, result.Deleted);
        Assert.Equal(1, result.Enqueued);
        Assert.Equal(2, result.Failed);
        Assert.Equal(3, result.Processing);
        Assert.Equal(9, result.Queues);
        Assert.Equal(7, result.Recurring);
        Assert.Equal(4, result.Scheduled);
        Assert.Equal(8, result.Servers);
        Assert.Equal(6, result.Succeeded);
    }

    [Fact]
    public void HourlyFailedJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var counts = Enumerable.Range(0, 24);

        var dictionaryDates = counts.ToDictionary(x =>
        {
            var hour = now.AddHours(-x);
            return new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, DateTimeKind.Utc);
        });

        UseContextSavingChanges(context =>
        {
            foreach (var item in dictionaryDates)
                if (item.Value != 0)
                    context.Add(new HangfireCounter
                    {
                        Key = $"stats:failed:{item.Key:yyyy-MM-dd-HH}",
                        Value = item.Value,
                    });
        });

        var result = UseMonitoringApi(instance => instance.HourlyFailedJobs());

        Assert.NotNull(result);
        Assert.Equal(24, result.Count);

        Assert.All(result, item => Assert.Equal(dictionaryDates[item.Key], item.Value));
    }

    [Fact]
    public void HourlySucceededJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var counts = Enumerable.Range(0, 24);

        var dictionaryDates = counts.ToDictionary(x =>
        {
            var hour = now.AddHours(-x);
            return new DateTime(hour.Year, hour.Month, hour.Day, hour.Hour, 0, 0, DateTimeKind.Utc);
        });

        UseContextSavingChanges(context =>
        {
            foreach (var item in dictionaryDates)
                if (item.Value != 0)
                    context.Add(new HangfireCounter
                    {
                        Key = $"stats:succeeded:{item.Key:yyyy-MM-dd-HH}",
                        Value = item.Value,
                    });
        });

        var result = UseMonitoringApi(instance => instance.HourlySucceededJobs());

        Assert.NotNull(result);
        Assert.Equal(24, result.Count);

        Assert.All(result, item => Assert.Equal(dictionaryDates[item.Key], item.Value));
    }

    [Fact]
    public void JobDetails_Throws_WhenJobIdParameterIsNull()
    {
        string jobId = null;

        UseMonitoringApi(instance => Assert.Throws<ArgumentNullException>(nameof(jobId),
            () => instance.JobDetails(jobId)));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void JobDetails_ReturnsNull_WhenJobNotExists(string jobId)
    {
        var result = UseMonitoringApi(instance => instance.JobDetails(jobId));

        Assert.Null(result);
    }

    [Fact]
    public void JobDetails_ReturnsCorrectResult()
    {
        var createdAt = DateTime.UtcNow;
        var stateCreatedAt = createdAt.AddSeconds(1);
        var data = new Dictionary<string, string>
        {
            ["Name"] = "Value",
        };
        var parameters = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2",
        };
        var state = new HangfireState
        {
            Name = "State",
            Reason = "Reason",
            CreatedAt = stateCreatedAt,
            Data = data,
        };
        var job = new HangfireJob
        {
            CreatedAt = createdAt,
            InvocationData = CreateInvocationData(() => SampleMethod("argument")),
            ExpireAt = createdAt + new TimeSpan(1, 0, 0, 0),
            States =
            [
                state,
            ],
            Parameters = parameters.
                Select(x => new HangfireJobParameter
                {
                    Name = x.Key,
                    Value = x.Value,
                }).
                ToList(),
        };
        UseContextSavingChanges(context => context.Add(job));
        var jobId = job.Id.ToString(CultureInfo.InvariantCulture);

        var result = UseMonitoringApi(instance => instance.JobDetails(jobId));

        Assert.NotNull(result);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(createdAt.AddDays(1), result.ExpireAt);
        Assert.Equal(typeof(EFCoreStorageTest), result.Job.Type);
        Assert.Equal(nameof(SampleMethod), result.Job.Method.Name);
        Assert.Equal(["argument"], result.Job.Args);
        Assert.NotNull(result.History);
        var historyItem = result.History.Single();
        Assert.Equal("State", historyItem.StateName);
        Assert.Equal("Reason", historyItem.Reason);
        Assert.Equal(stateCreatedAt, historyItem.CreatedAt);
        Assert.Equal(data, historyItem.Data);
        Assert.NotNull(result.Properties);
        Assert.Equal(parameters, result.Properties);
    }

    [Fact]
    public void ProcessingCount_ReturnsCorrectResult()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, ProcessingState.StateName);
        });

        var result = UseMonitoringApi(instance => instance.ProcessingCount());

        Assert.Equal(3, result);
    }

    [Fact]
    public void ProcessingJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var data = new Dictionary<string, string>
        {
            ["StartedAt"] = JobHelper.SerializeDateTime(now),
            ["ServerId"] = "ServerId",
            ["ServerName"] = "ServerName",
        };
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 5).
            Select(x =>
            {
                var job = new HangfireJob
                {
                    CreatedAt = now + new TimeSpan(0, 0, x),
                    InvocationData = invocationData,
                };
                var state = new HangfireState
                {
                    CreatedAt = DateTime.UtcNow,
                    Name = ProcessingState.StateName,
                    Data = data,
                };
                job.States.Add(state);
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.ProcessingJobs(1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.True(value.InProcessingState);
            Assert.Equal(now, value.StartedAt);
            Assert.Equal("ServerId", value.ServerId);
        });

        Assert.Equal(jobs[2].Id.ToString(CultureInfo.InvariantCulture), result[1].Key);
        Assert.Equal(jobs[3].Id.ToString(CultureInfo.InvariantCulture), result[0].Key);
    }

    [Fact]
    public void Queues_ReturnsEmptyList_WhenNoQueuesExists()
    {
        var result = UseMonitoringApi(instance => instance.Queues());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Queues_ReturnsCorrectList()
    {
        var queues = new[]
        {
            "queue1",
            "queue2",
        };
        var jobs = queues.Select(x => new HangfireJob
        {
            CreatedAt = DateTime.UtcNow,
            InvocationData = CreateInvocationData(() => SampleMethod(null)),
            QueuedJobs =
            [
                new()
                {
                    Queue = x,
                }
            ],
        });
        UseContextSavingChanges(context => context.AddRange(jobs));

        var result = UseMonitoringApi(instance => instance.Queues());

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ScheduledCount_ReturnsCorrectResult()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, ScheduledState.StateName);
        });

        var result = UseMonitoringApi(instance => instance.ScheduledCount());

        Assert.Equal(3, result);
    }

    [Fact]
    public void ScheduledJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var data = new Dictionary<string, string>
        {
            ["EnqueueAt"] = JobHelper.SerializeDateTime(now),
            ["ScheduledAt"] = JobHelper.SerializeDateTime(now.AddSeconds(1)),
        };
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 5).
            Select(x =>
            {
                var job = new HangfireJob
                {
                    CreatedAt = now + new TimeSpan(0, 0, x),
                    InvocationData = invocationData,
                };
                var state = new HangfireState
                {
                    CreatedAt = DateTime.UtcNow,
                    Name = ScheduledState.StateName,
                    Data = data,
                };
                job.States.Add(state);
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.ScheduledJobs(1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(now, value.EnqueueAt);
            Assert.Equal(now.AddSeconds(1), value.ScheduledAt);
        });

        Assert.Equal(jobs[2].Id.ToString(CultureInfo.InvariantCulture), result[1].Key);
        Assert.Equal(jobs[3].Id.ToString(CultureInfo.InvariantCulture), result[0].Key);
    }

    [Fact]
    public void Servers_ReturnsEmptyList_WhenNoServersExists()
    {
        var result = UseMonitoringApi(instance => instance.Servers());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Servers_ReturnsCorrectList()
    {
        var now = DateTime.UtcNow;
        string serverId1 = "server1";
        string serverId2 = "server2";
        var workerCount = 4;
        var startedAt1 = now - new TimeSpan(2, 0, 0, 0);
        var startedAt2 = now - new TimeSpan(1, 0, 0, 0); ;
        var heartbeat = now - new TimeSpan(1, 0, 0);
        var queues = new[]
        {
            "queue1",
            "queue1",
        };
        var queuesJson = SerializationHelper.Serialize(queues);
        var servers = new[]
        {
                new HangfireServer
                {
                    Id = serverId1,
                    StartedAt = startedAt1,
                    Heartbeat = heartbeat,
                    WorkerCount = workerCount,
                    Queues = queues,
                },
                new HangfireServer
                {
                    Id = serverId2,
                    StartedAt = startedAt2,
                    Heartbeat = heartbeat,
                    Queues = EmptyArrayStub,
                },
            };

        UseContextSavingChanges(context => context.AddRange(servers));

        var result = UseMonitoringApi(instance => instance.Servers());

        Assert.Equal(2, servers.Length);
        var server1 = result.Single(x => x.Name == serverId1);
        var server2 = result.Single(x => x.Name == serverId2);
        Assert.Equal(heartbeat, server1.Heartbeat);
        Assert.Equal(workerCount, server1.WorkersCount);
        Assert.Equal(queues, server1.Queues);
        Assert.Equal(startedAt1, server1.StartedAt);
        Assert.Equal(heartbeat, server2.Heartbeat);
        Assert.Equal(0, server2.WorkersCount);
        Assert.False(server2.Queues?.Any());
        Assert.Equal(startedAt2, server2.StartedAt);
    }

    [Fact]
    public void SucceededByDatesCount_ReturnsCorrectResult()
    {
        var today = DateTime.UtcNow.Date;
        var counts = Enumerable.Range(0, 7);
        var dictionaryDates = counts.ToDictionary(x => today.AddDays(-x));

        UseContextSavingChanges(context => context.AddRange(
            dictionaryDates.Select(item => new HangfireCounter
            {
                Key = $"stats:succeeded:{item.Key:yyyy-MM-dd}",
                Value = item.Value,
            })));

        var result = UseMonitoringApi(instance => instance.SucceededByDatesCount());

        Assert.NotNull(result);
        Assert.Equal(7, result.Count);
        Assert.All(result, item => Assert.Equal(dictionaryDates[item.Key], item.Value));
    }

    [Fact]
    public void SucceededJobs_ReturnsCorrectResult()
    {
        var now = DateTime.UtcNow;
        var data = new Dictionary<string, string>
        {
            ["SucceededAt"] = JobHelper.SerializeDateTime(now),
            ["PerformanceDuration"] = "123",
            ["Latency"] = "456",
            ["Result"] = "789",
        };
        var invocationData = CreateInvocationData(() => SampleMethod("Arguments"));
        var jobs = Enumerable.Range(0, 5).
            Select(x =>
            {
                var job = new HangfireJob
                {
                    CreatedAt = now + new TimeSpan(0, 0, x),
                    InvocationData = invocationData,
                };
                var state = new HangfireState
                {
                    CreatedAt = DateTime.UtcNow,
                    Name = SucceededState.StateName,
                    Data = data,
                };
                job.States.Add(state);
                return job;
            }).
            ToArray();
        UseContextSavingChanges(context =>
        {
            foreach (var job in jobs)
                context.Add(job);
            context.SaveChanges();
            foreach (var job in jobs)
            {
                var state = job.States.Single();
                job.State = state;
                job.StateName = state.Name;
            }
        });

        var result = UseMonitoringApi(instance => instance.SucceededJobs(1, 2));

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.All(result, item =>
        {
            Assert.NotNull(item.Key);
            var value = item.Value;
            Assert.NotNull(value);
            Assert.Equal(123 + 456, value.TotalDuration);
            Assert.Equal("789", value.Result);
            Assert.Equal(now, value.SucceededAt);
        });

        Assert.Equal(jobs[2].Id.ToString(CultureInfo.InvariantCulture), result[1].Key);
        Assert.Equal(jobs[3].Id.ToString(CultureInfo.InvariantCulture), result[0].Key);
    }

    [Fact]
    public void SucceededListCount_ReturnsCorrectResult()
    {
        UseContextSavingChanges(context =>
        {
            for (int i = 0; i < 3; i++)
                AddJobWithStateToContext(context, SucceededState.StateName);
        });

        var result = UseMonitoringApi(instance => instance.SucceededListCount());

        Assert.Equal(3, result);
    }

    private static void AddJobWithQueueItemToContext(HangfireContext context, string queue)
    {
        var job = new HangfireJob
        {
            CreatedAt = DateTime.UtcNow,
            InvocationData = CreateInvocationData(() => SampleMethod(null)),
        };
        var queueItem = new HangfireQueuedJob
        {
            Job = job,
            Queue = queue,
        };
        context.Add(job);
        context.Add(queueItem);
    }

    private static void AddJobWithStateToContext(
        HangfireContext context,
        string stateName,
        IDictionary<string, string> data = null)
    {
        data ??= new Dictionary<string, string>();
        var state = new HangfireState
        {
            CreatedAt = DateTime.UtcNow,
            Name = stateName,
            Data = data,
        };
        var job = new HangfireJob
        {
            CreatedAt = DateTime.UtcNow,
            InvocationData = CreateInvocationData(() => SampleMethod(null)),
            States =
            [
                state,
            ],
        };
        context.Add(job);
        context.SaveChanges();
        job.State = state;
        job.StateName = state.Name;
        context.SaveChanges();
    }

    private T UseMonitoringApi<T>(Func<EFCoreStorageMonitoringApi, T> func)
    {
        return func(new EFCoreStorageMonitoringApi(Storage));
    }
}
