﻿// <copyright file="SqlServerEventStore.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Projections.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using System.Web.Script.Serialization;
    using Sdk;

    /// <summary>
    /// Represents the SQL Server event store (for the event dispatcher).
    /// </summary>
    public class SqlServerEventStore : IEventStore
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        private readonly string connectionString;
        private readonly string schema;
        private readonly Guid partition;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        public SqlServerEventStore(string connectionString)
            : this(connectionString, "dbo", Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        public SqlServerEventStore(string connectionString, string schema)
            : this(connectionString, schema, Guid.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerEventStore(string connectionString, Guid partition)
            : this(connectionString, "dbo", partition)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlServerEventStore"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="schema">The schema.</param>
        /// <param name="partition">The partition.</param>
        internal SqlServerEventStore(string connectionString, string schema, Guid partition)
        {
            Guard.Against.NullOrEmpty(() => schema);

            this.connectionString = connectionString;
            this.schema = schema;
            this.partition = partition;

            var connection = new SqlConnection(connectionString);
            connection.InitializeSchema(schema, "SqlServerPersistence");
            connection.InitializeSchema(schema, typeof(SqlServerEventStore));

            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        /// <summary>
        /// Gets the events from the specified sequence number.
        /// </summary>
        /// <param name="sequenceNumber">The sequence number.</param>
        /// <returns>The events.</returns>
        public IEnumerable<object> GetEventsFrom(long sequenceNumber)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
            using (var connection = new SqlConnection(this.connectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = string.Concat(this.schema, ".GetEventsFrom");
                command.Parameters.Add("@SequenceNumber", SqlDbType.Int).Value = sequenceNumber;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    // TODO (Cameron): This is massively inefficient.
                    while (reader.Read())
                    {
                        var payloadTypeName = Convert.ToString(reader["PayloadTypeName"]);
                        var payloadType = Type.GetType(payloadTypeName);
                        var @event = Serializer.Deserialize(Convert.ToString(reader["Payload"]), payloadType);

                        yield return @event;
                    }
                }
            }
        }
    }
}