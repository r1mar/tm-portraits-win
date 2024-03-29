﻿using Autofac;
using Autofac.Integration.Mvc;
using System.Web.Mvc;

namespace TuRM.Portrait.Other
{
    public class AutofacConfig<T> where T : Module, new()
    {
        public static IContainer ConfigureContainer()
        {
            var builder = new ContainerBuilder();

            // Register dependencies in controllers
            builder.RegisterControllers(typeof(MvcApplication).Assembly);

            // Register dependencies in filter attributes
            builder.RegisterFilterProvider();

            // Register dependencies in custom views
            builder.RegisterSource(new ViewRegistrationSource());

            // Register our Data dependencies
            builder.RegisterModule(new T());

            var container = builder.Build();

            // Set MVC DI resolver to use our Autofac container
            DependencyResolver.SetResolver(new AutofacDependencyResolver(container));

            return container;
        }
    }
}