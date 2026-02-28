// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace SchemaNode.AI.Ontology;

/// <summary>
/// The semantic kind of the ownership / scoping relationship derived from an App's <c>ScopePolicy</c>.
/// </summary>
public enum OntologyScopeRelationKind
{
    /// <summary>No scope relationship — <c>SystemLevel</c> apps carry no owner semantics.</summary>
    None,

    /// <summary>
    /// Composition — lifecycle-bound, exactly-one ownership (<c>BusinessTarget</c>).
    /// The App data cannot exist without its owner entity.
    /// Rendered as <c>owl:FunctionalProperty</c>, <c>rdfs:subPropertyOf schema:isPartOf</c>, cardinality 1.
    /// </summary>
    Composition,

    /// <summary>
    /// Aggregation — scoped to a single shared context dimension (<c>IsolationContext</c>, one map).
    /// Ownership is shared but not lifecycle-bound.
    /// Rendered as <c>rdfs:subPropertyOf schema:isPartOf</c> with <c>owl:minCardinality 1</c>.
    /// </summary>
    Aggregation,

    /// <summary>
    /// Association — independently scoped across multiple context axes (<c>IsolationContext</c>, many maps).
    /// Each axis is a required key but no single entity "owns" the data.
    /// Rendered as plain <c>owl:ObjectProperty</c> per axis, with <c>owl:minCardinality 1</c>.
    /// </summary>
    Association,
}

/// <summary>
/// Whether an ontology property maps to a literal value (XSD type) or another class.
/// </summary>
public enum OntologyPropertyKind
{
    /// <summary>Data property — range is an XSD literal type (scalar) or an enum class.</summary>
    Data,

    /// <summary>Object property — range is a struct-based entity class.</summary>
    Object,
}

/// <summary>
/// A language-tagged label drawn from <c>LocaleString</c>.
/// The default key is emitted without a language tag; each <c>Trans</c> entry carries its tag.
/// </summary>
public class OntologyLabel
{
    /// <summary>Display text.</summary>
    public required string Value { get; init; }

    /// <summary>BCP-47 language tag (e.g. <c>"en"</c>, <c>"zh"</c>), or <see langword="null"/> for the default key.</summary>
    public string? Language { get; init; }
}

/// <summary>
/// An OWL class representing an App domain module (from <see cref="Runtime.AppType"/>).
/// Each App is a domain data container; its fields map to individual database tables.
/// </summary>
public class OntologyAppClass
{
    /// <summary>Turtle-safe local name (dots → underscores), e.g. <c>"gevent_finance"</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI of this class.</summary>
    public required string Iri { get; init; }

    /// <summary>Multi-language labels from <c>AppType.Display</c>.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Description from <c>AppType.Desc</c>.</summary>
    public string? Comment { get; init; }

    /// <summary>Parent app class IRI (rdfs:subClassOf) for nested apps.</summary>
    public string? ParentIri { get; init; }

    /// <summary>
    /// One entry per <see cref="Runtime.AppFieldType"/> —
    /// each field represents a database table whose schema is described by an <see cref="OntologyEntityClass"/>.
    /// </summary>
    public List<OntologyTableField> Tables { get; init; } = [];

    /// <summary>Field relation annotations from <see cref="Runtime.AppRelationSchema"/>.</summary>
    public List<OntologyRelation> Relations { get; init; } = [];

    /// <summary>
    /// Scope-derived ownership / context-scoping <c>owl:ObjectProperty</c> pairs generated from
    /// the App's <c>ScopePolicy</c>. These are first-class relations, independent of field definitions.
    /// </summary>
    public List<OntologyScopeRelation> ScopeRelations { get; init; } = [];
}

/// <summary>
/// Describes one App field, which corresponds to a database table.
/// The actual column schema is described by the <see cref="OntologyEntityClass"/> pointed to by <see cref="RangeIri"/>.
/// </summary>
public class OntologyTableField
{
    /// <summary>Original field name (camelCase, as defined in the schema).</summary>
    public required string Name { get; init; }

    /// <summary>Multi-language labels from <c>AppFieldType.Display</c>.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Description from <c>AppFieldType.Desc</c>.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Prefixed range IRI emitted directly in templates, e.g.
    /// <c>"app:gevent_finance_refund_Refund"</c>, <c>"xsd:string"</c>, or <c>"en:some_enum"</c>.
    /// </summary>
    public required string RangeIri { get; init; }

    /// <summary>Object when range is a struct/entity class; Data when scalar or enum.</summary>
    public OntologyPropertyKind Kind { get; init; }

    /// <summary>True when the underlying schema type is an array (table has multiple rows).</summary>
    public bool IsMultiValued { get; init; }

    /// <summary>True when the field has a push/calc function (derived value).</summary>
    public bool IsComputed { get; init; }
}

/// <summary>
/// An OWL class representing the schema of a data table (from <see cref="Runtime.StructType"/>).
/// Each property maps to a column.
/// </summary>
public class OntologyEntityClass
{
    /// <summary>Turtle-safe local name, e.g. <c>"gevent_finance_refund_Refund"</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI of this class.</summary>
    public required string Iri { get; init; }

    /// <summary>Multi-language labels from <c>AnySchemaType.Display</c>.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Full IRI of the base struct class, if the struct inherits from another (rdfs:subClassOf).</summary>
    public string? BaseClassIri { get; init; }

    /// <summary>All column definitions of this entity.</summary>
    public List<OntologyEntityProperty> Properties { get; init; } = [];
}

/// <summary>
/// A column/field inside an <see cref="OntologyEntityClass"/>, derived from <see cref="Schema.StructFieldConfig"/>.
/// </summary>
public class OntologyEntityProperty
{
    /// <summary>Original field name as defined in the struct schema.</summary>
    public required string Name { get; init; }

    /// <summary>Multi-language labels from <c>StructFieldConfig.Display</c>.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Description from <c>StructFieldConfig.Desc</c>.</summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Prefixed range IRI, e.g. <c>"xsd:decimal"</c>, <c>"en:status_enum"</c>,
    /// or <c>"app:some_struct"</c> for nested objects.
    /// </summary>
    public required string RangeIri { get; init; }

    /// <summary>Data for scalar/enum columns; Object for nested struct columns.</summary>
    public OntologyPropertyKind Kind { get; init; }

    /// <summary>True when the field has the <c>Require</c> flag.</summary>
    public bool IsRequired { get; init; }

    /// <summary>True when the field type is an array (multi-value column).</summary>
    public bool IsMultiValued { get; init; }

    /// <summary>
    /// True when the field name matches a foreign-key pattern (e.g. <c>eventId</c>, <c>personId</c>).
    /// The template emits an additional inferred <c>owl:ObjectProperty</c> alongside the literal property.
    /// </summary>
    public bool IsForeignKey { get; init; }

    /// <summary>
    /// When <see cref="IsForeignKey"/> is <see langword="true"/>, holds the stripped
    /// semantic name (e.g. <c>"event"</c> for field <c>"eventId"</c>).
    /// </summary>
    public string? SemanticName { get; init; }
}

/// <summary>
/// A single value (individual) inside an <see cref="OntologyEnumClass"/> SKOS ConceptScheme.
/// </summary>
public class OntologyEnumValue
{
    /// <summary>The concrete value token stored in the database (e.g. <c>"pending"</c>).</summary>
    public required string Value { get; init; }

    /// <summary>Multi-language display labels from <c>EnumValueInfo.Name</c>.</summary>
    public OntologyLabel[] Labels { get; init; } = [];
}

/// <summary>
/// An enum type class derived from <see cref="Runtime.EnumType"/>.
/// Rendered as a <c>skos:ConceptScheme</c> with one <c>skos:Concept</c> per value.
/// </summary>
public class OntologyEnumClass
{
    /// <summary>Turtle-safe local name.</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI of this class.</summary>
    public required string Iri { get; init; }

    /// <summary>Multi-language labels.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Top-level enumeration values as SKOS Concepts.</summary>
    public OntologyEnumValue[] Values { get; init; } = [];
}

/// <summary>
/// A semantic relation annotation derived from <see cref="Runtime.AppRelationSchema"/>.
/// </summary>
public class OntologyRelation
{
    /// <summary>Subject field path (e.g. <c>"appField"</c> or <c>"appField.dataField"</c>).</summary>
    public required string Field { get; init; }

    /// <summary><see cref="Enum.RelationType"/> label.</summary>
    public required string RelationType { get; init; }

    /// <summary>Function name that computes the relation.</summary>
    public required string Function { get; init; }

    /// <summary>Argument field paths or literal values.</summary>
    public string[] Args { get; init; } = [];
}

/// <summary>
/// An <c>owl:ObjectProperty</c> pair (forward + inverse) derived from an App's <c>ScopePolicy</c>,
/// representing the ownership / context-scoping relationship between an App data container and
/// the entity that owns or isolates it.
/// <para>
/// Scope relations are first-class citizens in the ontology — they are not derived from
/// <c>AppFieldType</c> definitions but directly from the structural semantics of the scope policy.
/// </para>
/// </summary>
public class OntologyScopeRelation
{
    /// <summary>
    /// Prefixed IRI of the forward property (e.g. <c>prop:MyApp.target</c>).
    /// Semantics: App —[forward]→ ContextEntity.
    /// </summary>
    public required string ForwardProperty { get; init; }

    /// <summary>
    /// Prefixed IRI of the inverse property (e.g. <c>prop:MyApp.targetOf</c>).
    /// Semantics: ContextEntity —[inverse]→ App.
    /// </summary>
    public required string InverseProperty { get; init; }

    /// <summary>Domain IRI of the forward property — the App class.</summary>
    public required string DomainIri { get; init; }

    /// <summary>
    /// Range IRI of the forward property — the context entity class (e.g. <c>app:TargetEntity</c>).
    /// </summary>
    public required string RangeIri { get; init; }

    /// <summary>Semantic relationship kind: <see cref="OntologyScopeRelationKind.Composition"/>, Aggregation, or Association.</summary>
    public OntologyScopeRelationKind Kind { get; init; }

    /// <summary>
    /// For <see cref="OntologyScopeRelationKind.Composition"/> and single-dimension
    /// <see cref="OntologyScopeRelationKind.Aggregation"/>: <c>schema:isPartOf</c>.
    /// <see langword="null"/> for plain <see cref="OntologyScopeRelationKind.Association"/>.
    /// </summary>
    public string? SubPropertyOf { get; init; }

    /// <summary>
    /// <see langword="true"/> for <see cref="OntologyScopeRelationKind.Composition"/> (BusinessTarget):
    /// exactly one owner, rendered as <c>owl:FunctionalProperty</c> with cardinality 1.
    /// </summary>
    public bool IsFunctional { get; init; }

    /// <summary>Original context item path from the schema (e.g. <c>"Access.Target"</c>, <c>"Access.OrgId"</c>).</summary>
    public string? ContextItem { get; init; }
}

/// <summary>
/// An abstract <c>owl:Class</c> placeholder representing a context ownership entity
/// (business target, organisation, tenant, etc.) that is referenced by
/// <see cref="OntologyScopeRelation"/> properties but whose own schema is external to this App.
/// </summary>
public class OntologyContextEntityClass
{
    /// <summary>Turtle-safe local name (e.g. <c>"TargetEntity"</c>, <c>"Org"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI of this class.</summary>
    public required string Iri { get; init; }

    /// <summary>Optional human-readable comment describing the entity's role.</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// A scalar (primitive) type derived from <see cref="Runtime.ScalarType"/>.
/// Rendered as <c>rdfs:Datatype</c> with optional restriction facets.
/// </summary>
public class OntologyScalarClass
{
    /// <summary>Turtle-safe local name.</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI.</summary>
    public required string Iri { get; init; }

    /// <summary>Multi-language labels.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Mapped XSD base type, or <see langword="null"/> when unconstrained.</summary>
    public string? BaseType { get; init; }

    /// <summary>Minimum allowed value, or <see langword="null"/> if unrestricted.</summary>
    public decimal? LowLimit { get; init; }

    /// <summary>Maximum allowed value, or <see langword="null"/> if unrestricted.</summary>
    public decimal? UpLimit { get; init; }

    /// <summary>Display unit label key, or <see langword="null"/>.</summary>
    public string? Unit { get; init; }

    /// <summary>Validation regex pattern, or <see langword="null"/>.</summary>
    public string? Regex { get; init; }
}

/// <summary>
/// One parameter of an <see cref="OntologyFunctionClass"/>.
/// </summary>
public class OntologyFunctionArg
{
    /// <summary>Parameter name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Resolved type string: prefixed IRI (<c>app:</c>, <c>en:</c>) or XSD literal type.
    /// Falls back to the raw schema type token when resolution is unavailable.
    /// </summary>
    public required string TypeStr { get; init; }

    /// <summary>Multi-language display labels.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Whether the argument is optional (<c>nullable</c>).</summary>
    public bool IsNullable { get; init; }

    /// <summary>Whether the argument is a variadic (<c>params</c>) parameter.</summary>
    public bool IsParams { get; init; }
}

/// <summary>
/// A function schema derived from <see cref="Runtime.FunctionType"/>.
/// Rendered as a typed function descriptor with argument and return-type contracts.
/// </summary>
public class OntologyFunctionClass
{
    /// <summary>Turtle-safe local name.</summary>
    public required string Name { get; init; }

    /// <summary>Full absolute IRI (under the <c>prop:</c> prefix).</summary>
    public required string Iri { get; init; }

    /// <summary>Multi-language labels.</summary>
    public OntologyLabel[] Labels { get; init; } = [];

    /// <summary>Ordered argument descriptors.</summary>
    public OntologyFunctionArg[] Args { get; init; } = [];

    /// <summary>
    /// Return type string: prefixed IRI or XSD type.
    /// <c>"void"</c> when the function produces no return value.
    /// </summary>
    public string ReturnTypeStr { get; init; } = "void";

    /// <summary>True when the function has no observable side effects.</summary>
    public bool IsPure { get; init; }

    /// <summary>True when the function acts as a type converter.</summary>
    public bool IsConverter { get; init; }

    /// <summary>True when the function is restricted to workflow execution contexts.</summary>
    public bool IsWorkflowOnly { get; init; }

    /// <summary>True when the function explicitly declares side effects.</summary>
    public bool HasSideEffect { get; init; }
}

/// <summary>
/// The complete ontology graph for an App schema hierarchy.
/// </summary>
public class OntologyGraph
{
    /// <summary>Base IRI namespace root (always ends with <c>/</c>).</summary>
    public required string BaseUri { get; init; }

    /// <summary>Entry-point app name.</summary>
    public required string AppName { get; init; }

    /// <summary>App domain classes (one per <c>AppType</c> in the traversed hierarchy).</summary>
    public List<OntologyAppClass> AppClasses { get; init; } = [];

    /// <summary>
    /// Entity (struct-based) classes referenced by app table fields or nested struct properties.
    /// Each describes the schema of one database table.
    /// </summary>
    public List<OntologyEntityClass> EntityClasses { get; init; } = [];

    /// <summary>Enum classes referenced by data properties.</summary>
    public List<OntologyEnumClass> EnumClasses { get; init; } = [];

    /// <summary>
    /// Scalar type classes directly loaded from schema (not inferred from struct fields).
    /// Rendered as <c>rdfs:Datatype</c> with optional facet restrictions.
    /// </summary>
    public List<OntologyScalarClass> ScalarClasses { get; init; } = [];

    /// <summary>
    /// Function classes directly loaded from schema.
    /// Rendered as typed function descriptors with argument / return-type contracts.
    /// </summary>
    public List<OntologyFunctionClass> FunctionClasses { get; init; } = [];

    /// <summary>
    /// Abstract context-entity classes generated from <c>ScopePolicy</c> references
    /// (e.g. <c>app:TargetEntity</c>, <c>app:Org</c>). Rendered as plain <c>owl:Class</c>
    /// declarations so that scope-relation range IRIs resolve correctly.
    /// </summary>
    public List<OntologyContextEntityClass> ContextEntityClasses { get; init; } = [];

    /// <summary>IRI prefix for app and entity classes (<c>{BaseUri}app/</c>).</summary>
    public string AppPrefix => $"{BaseUri}app/";

    /// <summary>IRI prefix for properties (<c>{BaseUri}prop/</c>).</summary>
    public string PropPrefix => $"{BaseUri}prop/";

    /// <summary>IRI prefix for enum classes (<c>{BaseUri}enum/</c>).</summary>
    public string EnumPrefix => $"{BaseUri}enum/";
}
