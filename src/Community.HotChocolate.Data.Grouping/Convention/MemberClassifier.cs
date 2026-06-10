using System.Reflection;
using HotChocolate.Data.Grouping.Fields;
using static HotChocolate.Data.Grouping.Expressions.ExpressionUtilities;

namespace HotChocolate.Data.Grouping.Convention;

internal static class MemberClassifier
{
    public static MemberClassification Classify(PropertyInfo property, IGroupingConvention convention)
        => Classify(property.PropertyType, convention);

    public static MemberClassification Classify(Type propertyType, IGroupingConvention convention)
    {
        if (TryGetCollectionElementType(propertyType, out var elementType))
        {
            var unwrappedElement = Nullable.GetUnderlyingType(elementType!) ?? elementType!;
            return new MemberClassification(unwrappedElement, IsCollection: true, IsElementScalar: convention.IsScalar(unwrappedElement));
        }

        var unwrapped = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return new MemberClassification(unwrapped, IsCollection: false, IsElementScalar: convention.IsScalar(unwrapped));
    }
}

internal readonly record struct MemberClassification(
    Type ElementClrType,
    bool IsCollection,
    bool IsElementScalar)
{
    public PathSegmentKind Kind => (IsCollection, IsElementScalar) switch
    {
        (true, true) => PathSegmentKind.PrimitiveCollection,
        (true, false) => PathSegmentKind.ObjectCollection,
        (false, true) => PathSegmentKind.Scalar,
        (false, false) => PathSegmentKind.ObjectNavigation,
    };
}
