// ReSharper disable InconsistentNaming

namespace SchemaNode.Utility;

/// <summary>
/// App-layer additional constants
/// </summary>
public static class AppConstant
{
    #region Schema Kind

    public const string SCHEMA_KIND_POLICY = "policy";
    public const string SCHEMA_KIND_EVENT = "event";

    public const int SCHEMA_KIND_ORDER_POLICY = 20;
    public const int SCHEMA_KIND_ORDER_EVENT = 21;

    #endregion

    #region App schema namespace roots

    public const string NS_APP_SCHEMA_POLICY = "system.schema.policy";
    public const string NS_APP_SCHEMA_EVENT  = "system.schema.event";
    public const string NS_APP_SCHEMA_FUNC   = "system.schema.func";

    #endregion

    #region App-side type reference strings

    public const string TYPE_REF_FUNC            = NS_APP_SCHEMA_FUNC   + ".schema";
    public const string TYPE_REF_POLICY          = NS_APP_SCHEMA_POLICY + ".schema";
    public const string TYPE_REF_RULE_EVALUATOR  = NS_APP_SCHEMA_FUNC   + ".schema";
    public const string TYPE_REF_RULE_PREDICATE  = NS_APP_SCHEMA_FUNC   + ".schema";

    #endregion
}
