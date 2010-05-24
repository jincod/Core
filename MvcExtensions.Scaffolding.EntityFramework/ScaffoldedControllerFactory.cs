﻿#region Copyright
// Copyright (c) 2009 - 2010, Kazi Manzur Rashid <kazimanzurrashid@gmail.com>.
// This source is subject to the Microsoft Public License. 
// See http://www.microsoft.com/opensource/licenses.mspx#Ms-PL. 
// All other rights reserved.
#endregion

namespace MvcExtensions.Scaffolding.EntityFramework
{
    using System;
    using System.Collections.Generic;
    using System.Data.Metadata.Edm;
    using System.Data.Objects;
    using System.Linq;
    using System.Web.Routing;

    /// <summary>
    /// Defines a controller factory which creates scaffolded controller.
    /// </summary>
    public class ScaffoldedControllerFactory : ExtendedControllerFactory
    {
        private static readonly Type genericControllerType = typeof(ScaffoldedController<,>);

        private static readonly object entityMapSyncLock = new object();
        private static IDictionary<string, EntityInfo> entityMap;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScaffoldedControllerFactory"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="actionInvokerRegistry">The action invoker registry.</param>
        /// <param name="database">The database.</param>
        public ScaffoldedControllerFactory(ContainerAdapter container, IActionInvokerRegistry actionInvokerRegistry, ObjectContext database) : base(container, actionInvokerRegistry)
        {
            Invariant.IsNotNull(database, "database");

            Database = database;
        }

        /// <summary>
        /// Gets the database.
        /// </summary>
        /// <value>The database.</value>
        protected ObjectContext Database
        {
            get;
            private set;
        }

        /// <summary>
        /// Retrieves the controller type for the specified name and request context.
        /// </summary>
        /// <param name="requestContext">The context of the HTTP request, which includes the HTTP context and route data.</param>
        /// <param name="controllerName">The name of the controller.</param>
        /// <returns>The controller type.</returns>
        protected override Type GetControllerType(RequestContext requestContext, string controllerName)
        {
            LoadEntityMap(Database);

            EntityInfo entityInfo;

            if (entityMap.TryGetValue(controllerName, out entityInfo))
            {
                Type controllerType = genericControllerType.MakeGenericType(entityInfo.EntityType, entityInfo.KeyType);

                return controllerType;
            }

            return base.GetControllerType(requestContext, controllerName);
        }

        private static void LoadEntityMap(ObjectContext database)
        {
            if (entityMap == null)
            {
                lock (entityMapSyncLock)
                {
                    if (entityMap == null)
                    {
                        entityMap = BuildEntityMap(database);
                    }
                }
            }
        }

        private static IDictionary<string, EntityInfo> BuildEntityMap(ObjectContext database)
        {
            IDictionary<string, EntityInfo> map = new Dictionary<string, EntityInfo>(StringComparer.OrdinalIgnoreCase);

            database.MetadataWorkspace.LoadFromAssembly(database.GetType().Assembly);

            EntityContainer container = database.MetadataWorkspace.GetEntityContainer(database.DefaultContainerName, DataSpace.CSpace);
            ObjectItemCollection objectSpaceItems = (ObjectItemCollection)database.MetadataWorkspace.GetItemCollection(DataSpace.OSpace);

            // We will only scaffold if entity has only one key
            foreach (EntitySet entitySet in container.BaseEntitySets.OfType<EntitySet>().Where(es => es.ElementType.KeyMembers.Count == 1))
            {
                EntityType entityType = (EntityType)database.MetadataWorkspace.GetObjectSpaceType(entitySet.ElementType);
                Type entityClrType = objectSpaceItems.GetClrType(entityType);
                Type keyClrType = ((PrimitiveType)entitySet.ElementType.KeyMembers.First().TypeUsage.EdmType).ClrEquivalentType;

                EntityInfo info = new EntityInfo { EntityType = entityClrType, KeyType = keyClrType };

                map.Add(entitySet.Name, info);
            }

            return map;
        }

        private sealed class EntityInfo
        {
            public Type EntityType { get; set; }

            public Type KeyType { get; set; }
        }
    }
}