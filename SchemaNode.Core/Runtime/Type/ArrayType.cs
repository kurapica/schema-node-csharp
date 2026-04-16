using System;
using System.Collections.Generic;
using System.Text;
using SchemaNode.Attribute;
using SchemaNode.Property.Schema;

namespace SchemaNode.Runtime;

public sealed class ArrayType : AnySchemaType
{
    public AnySchemaType? ElementSchemaType { get; private set; }
}
