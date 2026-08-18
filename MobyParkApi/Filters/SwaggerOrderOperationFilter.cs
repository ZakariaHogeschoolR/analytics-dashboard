using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class SwaggerOrderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var orderAttr = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(typeof(SwaggerOrderAttribute), false)
            .FirstOrDefault() as SwaggerOrderAttribute;

        if (orderAttr != null)
        {
            operation.Extensions.Add("x-swagger-order", new Microsoft.OpenApi.Any.OpenApiInteger(orderAttr.Order));
        }
    }
}
