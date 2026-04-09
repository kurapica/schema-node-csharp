using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaNode.Runtime;

public sealed class ArrayType : AnySchemaType
{
    public AnySchemaType? ElementSchemaType { get; private set; }
}
