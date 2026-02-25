using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refahi.Modules.Catalog.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //var connectionStringName = configuration.GetValue<string>("ConnectionStringName");
        //var connectionString = configuration.GetConnectionString(connectionStringName);

        return services;
    }
}