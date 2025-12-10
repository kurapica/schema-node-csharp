using SchemaNode.Attribute;
using static SchemaNode.Utility.Constant;

namespace SchemaNode.Enum;

/// <summary>
/// The schema node status, for system diagnosis
/// </summary>
[Schema($"{NS_SYSTEM_SCHEMA}.status")]
public enum SchemaNodeStatus
{
    /// <summary>
    /// The node is ready
    /// </summary>
    Ready = 0,

    /// <summary>
    /// No type definition
    /// </summary>
    NoDefinition,

    /// <summary>
    /// No base scalar
    /// </summary>
    ScalarHasWrongBase,
    
    /// <summary>
    /// Scalar has wrong post valid func
    /// </summary>
    ScalarHasWrongPostValid,

    /// <summary>
    /// Scalar has wrong pre valid func
    /// </summary>
    ScalarHasWrongPreValid,

    /// <summary>
    /// Scalar has wrong white list func
    /// </summary>
    ScalarHasWrongWhiteList,

    /// <summary>
    /// No array element type
    /// </summary>
    ArrayHasWrongElementType,

    /// <summary>
    /// The array of struct has no primary
    /// </summary>
    ArrayHasNoPrimary,

    /// <summary>
    /// The array of strut has wrong primary
    /// </summary>
    ArrayHasWrongPrimary,

    /// <summary>
    /// The array of struct has wrong indexes
    /// </summary>
    ArrayHasWrongIndex,

    /// <summary>
    /// The array has wrong validation function
    /// </summary>
    ArrayHasWrongValid,

    /// <summary>
    /// The sturct type has no member
    /// </summary>
    StructNoMember,

    /// <summary>
    /// The struct type of wrong base,
    /// </summary>
    StructWrongBase,

    /// <summary>
    /// The struct type has wrong validation function
    /// </summary>
    StructHasWrongValid,

    /// <summary>
    /// The struct member has not type
    /// </summary>
    StructMemberWrongType,

    /// <summary>
    /// The struct member has wrong function
    /// </summary>
    StructMemberWrongFunc,

    /// <summary>
    /// The struct member has wrong validation function
    /// </summary>
    StructMemberWrongValidFunc,

    /// <summary>
    /// The struct relationship has wrong valdiation function
    /// </summary>
    StructRelationshipWrongFunc,

    /// <summary>
    /// The funciton has wrong return type
    /// </summary>
    FunctionWrongReturnType,

    /// <summary>
    /// The function argument require type
    /// </summary>
    FunctionArgumentNoType,

    /// <summary>
    /// The function argument has wrong type
    /// </summary>
    FunctionArgumentWrongType,

    /// <summary>
    /// The function argument has no name
    /// </summary>
    FunctionArgumentNoName,

    /// <summary>
    /// The function argument use duplicated name
    /// </summary>
    FunctionArgumentDuplicateName,

    /// <summary>
    /// The function expression return wrong type
    /// </summary>
    FunctionExpWrongType,

    /// <summary>
    /// The function expression call wrong function
    /// </summary>
    FunctionExpWrongFunc,
    
    /// <summary>
    /// The function expression call a invalid function
    /// </summary>
    FunctionExpInValidFunc,

    /// <summary>
    /// The function expression use wrong arguments
    /// </summary>
    FunctionExpWrongFuncArgs,
    
    /// <summary>
    /// The function has no expressions
    /// </summary>
    FunctionNoExps,

    /// <summary>
    /// The function expression has no name
    /// </summary>
    FunctionExpNoName,

    /// <summary>
    /// The function expression use duplicated name
    /// </summary>
    FunctionExpDuplicateName,
    
    /// <summary>
    /// The function expression use wrong func for reduce
    /// </summary>
    FunctionExpWrongFuncForReduce,
    
    /// <summary>
    /// The function expression use wrong func for first
    /// </summary>
    FunctionExpWrongFuncForFirst,
    
    /// <summary>
    /// The function expression use wrong func for last
    /// </summary>
    FunctionExpWrongFuncForLast,
    
    /// <summary>
    /// The function expression use wrong func for filter
    /// </summary>
    FunctionExpWrongFuncForFilter,
    
    /// <summary>
    /// The function return struct member type not valid
    /// </summary>
    FunctionReturnMemberNotValid,

    /// <summary>
    /// The function expression haven't pass the complier
    /// </summary>
    FunctionExpsHasCompileError,

    /// <summary>
    /// The workflow has wrong func
    /// </summary>
    WorkflowWrongFunc,
    
    /// <summary>
    /// The workflow has wrong event
    /// </summary>
    WorkflowWrongEvent,
    
    /// <summary>
    /// The policy has wrong func
    /// </summary>
    PolicyWrongFunc,
    
    /// <summary>
    /// The application invalid field
    /// </summary>
    ApplicationInvalidField,

    /// <summary>
    /// The application field wrong type
    /// </summary>
    ApplicationFieldWrongType,

    /// <summary>
    /// The application field wrong func
    /// </summary>
    ApplicationFieldWrongFunc,

    /// <summary>
    /// The application func wrong field
    /// </summary>
    ApplicationFieldWrongFuncField,

    /// <summary>
    /// The application field wrong reference
    /// </summary>
    ApplicationFieldWrongRef,

    /// <summary>
    /// The relation has wrong target field
    /// </summary>
    ApplicationRelationWrongTarget,

    /// <summary>
    /// The relation has wrong func
    /// </summary>
    ApplicationRelationWrongFunc,
    
    /// <summary>
    /// The application data auth wrong func
    /// </summary>
    ApplicationDataAuthWrongFunc,
    
    /// <summary>
    /// The application field data auth wrong func
    /// </summary>
    ApplicationFieldDataAuthWrongFunc,

    /// <summary>
    /// The application field data auth wrong field
    /// </summary>
    ApplicationFieldDataAuthWrongField,
}