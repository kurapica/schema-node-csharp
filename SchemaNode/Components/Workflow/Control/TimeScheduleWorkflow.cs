using Quartz;
using SchemaNode.Attribute;
using SchemaNode.Context;
using static SchemaNode.Utility.Constant;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace SchemaNode.Components;

[Schema($"{NS_SYSTEM_WORKFLOW}.control.scheduler")]
public class TimeScheduleWorkflow: Workflow,
    IWorkflowSession<JobKey>
{
    public async Task<JobKey?> ProcessAsync(WorkflowContext context, JobKey? jobKey, TimeSchedule schedule)
    {
        IScheduler scheduler = context.GetRequiredService<IScheduler>();
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

        var triggerBuilder = TriggerBuilder.Create().WithCronSchedule(schedule.Cron);
        if (schedule.End is not null)
            triggerBuilder.EndAt(new DateTimeOffset(schedule.End.Value));
        
        var timeTrigger = triggerBuilder
            .StartNow()
            .Build();
        await scheduler.ScheduleJob(timeJob, timeTrigger);
        return timeJob.Key;
    }
    
    /// <inheritdoc />
    public async Task ReleaseSessionAsync(WorkflowContext context, JobKey? session)
    {
        IScheduler scheduler = context.GetRequiredService<IScheduler>();
        if (session != null)
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
[Schema($"{NS_SYSTEM_WORKFLOW}.control.schedule")]
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
    [Schema(NS_SYSTEM_WORKFLOW_CRON)]
    public string Cron { get; set; } = string.Empty;
}
