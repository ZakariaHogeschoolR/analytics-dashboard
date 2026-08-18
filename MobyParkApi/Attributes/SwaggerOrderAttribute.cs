[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class SwaggerOrderAttribute : Attribute
{
    public int Order { get; }

    public SwaggerOrderAttribute(int order)
    {
        Order = order;
    }
}
