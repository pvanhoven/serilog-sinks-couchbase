﻿// Copyright 2014 Serilog Contributors
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Serilog.Debugging;
using Serilog.Sinks.PeriodicBatching;
using LogEvent = Serilog.Sinks.Couchbase.Data.LogEvent;

namespace Serilog.Sinks.Couchbase
{
    /// <summary>
    /// Writes log events as documents to a Couchbase database.
    /// </summary>
    public class CouchbaseSink : PeriodicBatchingSink
    {
        readonly IFormatProvider _formatProvider;
        readonly IBucket _bucket;

        /// <summary>
        /// A reasonable default for the number of events posted in
        /// each batch.
        /// </summary>
        public const int DefaultBatchPostingLimit = 50;

        /// <summary>
        /// A reasonable default time to wait between checking for event batches.
        /// </summary>
        public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="couchbaseUriList">A list of a Couchbase database servers.</param>
        /// <param name="bucketName">The bucket to store batches in.</param>
        /// <param name="batchPostingLimit">The maximum number of events to post in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
        public CouchbaseSink(string[] couchbaseUriList, string bucketName, int batchPostingLimit, TimeSpan period, IFormatProvider formatProvider)
            : base(batchPostingLimit, period)
        {
            if (couchbaseUriList == null) throw new ArgumentNullException("couchbaseUriList");
            if (couchbaseUriList.Length == 0) throw new ArgumentException("couchbaseUriList");
            if (couchbaseUriList[0] == null) throw new ArgumentNullException("couchbaseUriList");

            if (bucketName == null) throw new ArgumentNullException("bucketName");

            ClientConfiguration configuration = new ClientConfiguration
            {
                Servers = couchbaseUriList.Select(uri => new Uri(uri)).ToList(),
            };

            var cluster = new Cluster(configuration);
            _bucket = cluster.OpenBucket(bucketName);

            _formatProvider = formatProvider;
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            // First flush the buffer
            base.Dispose(disposing);

            if (disposing)
                _bucket.Dispose();
        }

        /// <summary>
        /// Emit a batch of log events.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>Override either <see cref="PeriodicBatchingSink.EmitBatch"/> or <see cref="PeriodicBatchingSink.EmitBatchAsync"/>,
        /// not both.</remarks>
        protected override void EmitBatch(IEnumerable<Events.LogEvent> events)
        {
            // This sink doesn't actually write batches, instead only using
            // the PeriodicBatching infrastructure to manage background work.
            // Probably needs modification.

            foreach (var logEvent in events)
            {
                var key = Guid.NewGuid().ToString();
                
                IOperationResult<LogEvent> result = _bucket.Insert(key, new LogEvent(logEvent, logEvent.RenderMessage(_formatProvider)));

                if (!result.Success)
                    SelfLog.WriteLine("Failed to store value");
            }
        }
    }
}
