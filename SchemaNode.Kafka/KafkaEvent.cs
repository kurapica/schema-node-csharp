using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchemaNode.Components;
using SchemaNode.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchemaNode.Kafka;

/// <summary>
/// THe Kafka Event
/// </summary>
public abstract class KafkaEvent: Event
{
}

/// <summary>
/// Kafka event with typed payload
/// </summary>
public abstract class KafkaEvent<TPayload> : KafkaEvent, IEventPayload<TPayload>
{
}


    /// <summary>
    /// 指定参会额度使用信息主题
    /// </summary>
    [KafkaTopic("attendence_join_topic")]
    public class AttendenceJoinEvent : KafkaEvent<AttendenceJoinPayload>
    {
    }

    /// <summary>
    /// 参会额度使用信息
    /// </summary>
    public class AttendenceJoinPayload
    {
        /// <summary>
        /// 会议ID
        /// </summary>
        public string BventId { get; set; }

        /// <summary>
        /// 额度类型
        /// </summary>
        public string AttendenceType { get; set; }

        /// <summary>
        /// 展商ID
        /// </summary>
        public string ExhibitorId { get; set; }
    }