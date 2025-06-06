﻿using Dapper;
using StackExchange.Profiling;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static System.Console;

namespace Samples.Console
{
    /// <summary>
    /// simple sample console application.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// application entry point.
        /// </summary>
        public static async Task Main()
        {
            try
            {
                await TestAsync();
                TestMultiThreaded();
                WriteLine(MiniProfiler.Current.RenderPlainText());

                if (Debugger.IsAttached)
                    ReadKey();
            }
            catch (Exception ex)
            {
                WriteLine(ex);
            }
        }

        /// <summary>
        /// Test the profiling.
        /// </summary>
        public static async Task TestAsync()
        {
            var mp = MiniProfiler.StartNew("Test");

            using (mp.Step("Level 1"))
            using (var conn = GetConnection())
            {
                conn.Query<long>("select 1");

                using (mp.Step("Level 2"))
                {
                    conn.Query<long>("select 1");
                }

                using (var client = new HttpClient())
                using (mp.CustomTiming("http", "GET https://google.com"))
                {
                    await client.GetAsync("https://google.com");
                }
            }

            await mp!.StopAsync();
            WriteLine(MiniProfiler.Current.RenderPlainText());

        }

        public static void TestMultiThreaded()
        {
            var mp = MiniProfiler.StartNew("Locking");
            static void doWork() => Thread.Sleep(new Random().Next(1, 50));

            using (mp.Step("outer"))
            {
                Parallel.For(0, 5, i =>
                {
                    doWork();

                    using (mp.Step("step " + i))
                    {
                        using (mp.CustomTiming("MyCustom", "test command"))
                        {
                            doWork();
                        }

                        using (mp.Step("sub-step" + i))
                        {
                            doWork();
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Returns an open connection that will have its queries profiled.
        /// </summary>
        /// <returns>the database connection abstraction</returns>
        public static DbConnection GetConnection()
        {
            DbConnection cnn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");

            // to get profiling times, we have to wrap whatever connection we're using in a ProfiledDbConnection
            // when MiniProfiler.Current is null, this connection will not record any database timings
            if (MiniProfiler.Current != null)
            {
                cnn = new StackExchange.Profiling.Data.ProfiledDbConnection(cnn, MiniProfiler.Current);
            }

            cnn.Open();
            return cnn;
        }
    }
}
