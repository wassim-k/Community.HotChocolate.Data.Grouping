using System.ComponentModel;

namespace HotChocolate.Data.Grouping.Fields;

/// <summary>
/// Schema-time classification of a property hop.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public enum PathSegmentKind
{
    /// <summary>Terminal leaf — registered scalar, enum, or custom scalar.</summary>
    Scalar,

    /// <summary>Scalar-property hop into a nested object.</summary>
    ObjectNavigation,

    /// <summary>Collection-of-objects hop — needs SelectMany flattening downstream.</summary>
    ObjectCollection,

    /// <summary>Collection-of-scalars hop — itself a terminal leaf.</summary>
    PrimitiveCollection,
}
