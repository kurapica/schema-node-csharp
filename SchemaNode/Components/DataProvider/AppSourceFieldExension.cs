using SchemaNode.Context;
using SchemaNode.Node;
using SchemaNode.Runtime;
using SchemaNode.Schema;

namespace SchemaNode.Components;

public static class AppSourceFieldExension
{
    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public static async Task<bool> SetSourceFieldNode(this SchemaContext context, AppFieldType field, string target, string sourceTarget)
    {
        if (field.SourceAppType == null) return false;

        AppType? appType = await context.GetAppTypeAsync(field.App);
        if (appType?.RefField == null) return false;

        (_, string oldTarget) = await GetSourceFieldNode(context, field, target, forPush: true);
        if (oldTarget == sourceTarget) return true;

        await context.SaveFieldEntityAsync(appType.RefField, target, new AppRef { App = field.SourceApp!, Target = sourceTarget });

        // Check track push fields
        foreach (AppFieldType trackPushField in appType.Fields!.Where(f => f.EnablePushTrackTable && f.SourceAppType == field.SourceAppType))
        {
            AppFieldType? refField = trackPushField.SourceFieldType;
            if (refField == null) continue;

            // Get the track data
            (AnySchemaNode? trackData, _) = await context.GetFieldDataAsync(trackPushField, target);
            if (trackData == null || trackData.IsEmpty) continue;

            // clear the track data from old target
            if (!string.IsNullOrWhiteSpace(oldTarget))
            {
                await context.SaveIncrementalData(refField, oldTarget, null, trackData);
            }

            // save to the new target
            if (!string.IsNullOrWhiteSpace(sourceTarget))
            {
                await context.SaveIncrementalData(refField, sourceTarget, trackData, null);
            }
        }

        return true;
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public static async Task<bool> SetSourceFieldNode(this SchemaContext context, AppType app, string target, string sourceApp, string sourceTarget)
    {
        AppFieldType? field = app.Fields?.FirstOrDefault(f => sourceApp.Equals(f.SourceApp, StringComparison.OrdinalIgnoreCase));
        return field == null || await SetSourceFieldNode(context, field, target, sourceTarget);
    }

    /// <summary>
    /// Sets the ref target of the field
    /// </summary>
    public static async Task<bool> SetSourceFieldNode(this SchemaContext context, string app, string target, string sourceApp, string sourceTarget)
    {
        AppType? node = await context.GetAppTypeAsync(app);
        return node == null || await SetSourceFieldNode(context, node, target, sourceApp, sourceTarget);
    }

    /// <summary>
    /// Gets the source field node
    /// </summary>
    public static async Task<(AppFieldType?, string)> GetSourceFieldNode(this SchemaContext context, AppFieldType? field, string target, bool forPush = false)
    {
        if (field is { EnablePushTrackTable: true } && !forPush) return (field, target);
        if (field?.SourceAppType == null) return (forPush ? null : field, target);
        AppType? appType = await context.GetAppTypeAsync(field.App);

        // Means the app is front only and use the source node's target as target
        if (appType?.RefField == null) return forPush ? (null, string.Empty) : await GetSourceFieldNode(context, field.SourceFieldType, target);

        (List<AppRef> refData, _) = await context.GetFieldEntitiesAsync<AppRef>(appType.RefField, target, e => e.App == field.SourceAppType.Name);
        if (refData is { Count: > 0 })
        {
            if (!string.IsNullOrWhiteSpace(refData[0].Target))
                return forPush ? (field.SourceFieldType, refData[0].Target!) : await GetSourceFieldNode(context, field.SourceFieldType, refData[0].Target!);
        }

        // Consider use the same target if no ref for view
        return forPush ? (null, string.Empty) : await GetSourceFieldNode(context, field.SourceFieldType, target);
    }

}
