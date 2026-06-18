using Quartz;
using SchemaNode.Attribute;
using SchemaNode.Context;
using SchemaNode.Property.Core;
using static SchemaNode.Utility.AppConstant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Workflow;

[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.scheduler")]
public class TimeScheduleWorkflow: BaseWorkflow,
    IWorkflowSession<JobKey>
{
    public async Task<JobKey?> ProcessAsync(WorkflowContext context, JobKey? jobKey, TimeSchedule schedule)
    {
        ISchedulerFactory factory = context.GetRequiredService<ISchedulerFactory>();
        IScheduler scheduler = await factory.GetScheduler();
        try
        {
            if (jobKey != null) await scheduler.DeleteJob(jobKey);
        }
        catch
        {
            // ignore
        }
        
        var now = DateTimeOffset.UtcNow;
        
        if (schedule.Start is not null && schedule.Start > now.DateTime)
        {
            var waitUtilJob = JobBuilder.Create<WaitUntil>()
                .UsingJobData(new JobDataMap
                {
                    { "schedule", new ScheduleJobData(context, this, schedule) }
                })
                .WithIdentity($"{context.Id}-{Name}-waitUntil", "timeSchedule")
                .Build();
            
            var waitUntilTrigger = TriggerBuilder.Create()
                .StartAt(new DateTimeOffset(schedule.Start.Value))
                .Build();
            
            await scheduler.ScheduleJob(waitUtilJob, waitUntilTrigger);
            return waitUtilJob.Key;
        }
        else if (schedule.End is not null && schedule.End <= now.DateTime)
        {
            context.Terminate(this);
            return null;
        }
        
        var timeJob = JobBuilder.Create<TimeJob>()
            .UsingJobData(new JobDataMap
            {
                { "schedule", new ScheduleJobData(context, this, schedule) }
            })
            .WithIdentity($"{context.Id}-{Name}-timeJob", "timeSchedule")
            .Build();

        var timeTrigger = schedule.End is not null
            ? TriggerBuilder.Create().WithCronSchedule(schedule.Cron)
                .EndAt(new DateTimeOffset(schedule.End.Value))
                .StartNow()
                .Build()
            : TriggerBuilder.Create().WithCronSchedule(schedule.Cron)
                .StartNow()
                .Build();
        await scheduler.ScheduleJob(timeJob, timeTrigger);
        return timeJob.Key;
    }
    
    /// <inheritdoc />
    public async Task ReleaseSessionAsync(WorkflowContext context, JobKey? session)
    {
        if (session == null) return;
        ISchedulerFactory factory = context.GetRequiredService<ISchedulerFactory>();
        IScheduler scheduler = await factory.GetScheduler();
        await scheduler.DeleteJob(session);
    }
    
    /// <summary>
    /// The wait until job
    /// </summary>
    class WaitUntil: IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            ScheduleJobData job = (ScheduleJobData)context.JobDetail.JobDataMap["schedule"];
            context.Scheduler.DeleteJob(context.JobDetail.Key);
            return job.Workflow.ProcessAsync(job.Context, context.JobDetail.Key, job.Schedule);
        }
    }
    
    /// <summary>
    /// The time job
    /// </summary>
    class TimeJob: IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            ScheduleJobData job = (ScheduleJobData)context.JobDetail.JobDataMap["schedule"];
            if (!job.Workflow.Fork || job.Schedule.End is not null && job.Schedule.End <= DateTimeOffset.UtcNow.DateTime)
            {
                context.Scheduler.DeleteJob(context.JobDetail.Key);
                job.Context.Terminate(job.Workflow); // terminate the workflow
            }
            else
                job.Context.Done(job.Workflow); // continue to the next node
            return Task.CompletedTask;
        }
    }
    
    record ScheduleJobData(WorkflowContext Context, TimeScheduleWorkflow Workflow, TimeSchedule Schedule);

}

/// <summary>
/// The time schedule definition
/// </summary>
[Meta<SchemaType>($"{NS_SYSTEM_WORKFLOW_CONTROL}.schedule")]
public class TimeSchedule
{
    /// <summary>
    /// The start time
    /// </summary>
    public DateTime? Start { get; set; }
    
    /// <summary>
    /// The end time
    /// </summary>
    public DateTime? End { get; set; }
    
    /// <summary>
    /// The cron expression
    /// </summary>
    [Meta<SchemaType>(typeof(Cron))]
    public string Cron { get; set; } = string.Empty;
}


/// <summary>
/// Represents the cron expression type for time scheduling.
/// </summary>
[Meta<SchemaType>(NS_SYSTEM_WORKFLOW_CRON)]
public class Cron : Scalar.String;