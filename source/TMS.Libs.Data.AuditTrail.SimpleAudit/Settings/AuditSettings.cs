using System.Collections.ObjectModel;

using TMS.Libs.Data.AuditTrail.SimpleAudit.Help;
using TMS.Libs.Data.AuditTrail.SimpleAudit.Models;

namespace TMS.Libs.Data.AuditTrail.SimpleAudit.Settings
{
    public sealed class AuditSettings
    {
        #region Vars

        private readonly Dictionary<Type, EntityAuditSettings> _entitiesSettings = new();
        private readonly SimpleAuditContext _dbContext;

        #endregion

        #region Private

        private PropertyAuditSettings GetPropertySettings(Type type, string propertyName)
            => new(
                propertyName,
                _dbContext.GetSQLColumnName(type, propertyName),
                _dbContext.GetColumnType(type, propertyName),
                _dbContext.GetColumnSQLType(type, propertyName));

        private EntityAuditSettings GetEntitySettings(Type type)
            => new(type, _dbContext.GetSQLTableName(type));

        #endregion

        #region Internal

        internal AuditSettings(SimpleAuditContext context)
            => _dbContext = context;

        internal bool ValidateIfAuditingConfigured()
        {
            if (AuditTrailTableModelType is null || AuditMappingCallBackAsync is null)
            {
                throw new InvalidOperationException("Auditing is not configured yet.");
            }
            return true;
        }

        internal bool HasEntitiesSettings => _entitiesSettings.Count > 0;

        internal EntityAuditSettings? Get(Type type)
            => _entitiesSettings
                .TryGetValue(type, out var settings) ? settings : null;

        internal PropertyAuditSettings? Get(EntityAuditSettings entitySettings, string name)
            => entitySettings
                .AuditableProperties
                .SingleOrDefault(ap => ap.PropertyName == name);

        /// <summary>
        /// Add or update table model and list of properties to Audit.
        /// </summary>
        internal void Set(
            Type type, 
            string? tableAlias, 
            List<string> propertiesNames)
        {
            if (!_entitiesSettings.TryGetValue(type, out var entitySettings))
            {
                entitySettings = GetEntitySettings(type);
                _entitiesSettings[type] = entitySettings;
            }

            entitySettings.TableAlias = tableAlias;

            foreach (var propertyName in propertiesNames)
            {
                var propertySettings = Get(entitySettings, propertyName);
                        
                if (propertySettings == null)
                {
                    propertySettings = GetPropertySettings(type, propertyName);
                    entitySettings.AuditableProperties.Add(propertySettings);
                }
                propertySettings.ValueMapper = null;
                propertySettings.ColumnNameAlias = null;
            }
        }

        /// <summary>
        /// Add or update the Audit settings for table model and singly property, with fine tunning.
        /// </summary>
        internal void Set(
            Type type, 
            string? tableAlias, 
            string propertyName, 
            Func<object?, object?>? valueMapper, 
            string? columnAlias)
        {
            if (!_entitiesSettings.TryGetValue(type, out var entitySettings))
            {
                entitySettings = GetEntitySettings(type);
                _entitiesSettings[type] = entitySettings;
            }

            entitySettings.TableAlias = tableAlias;

            var propertySettings = Get(entitySettings, propertyName);
            if (propertySettings == null)
            {
                propertySettings = GetPropertySettings(type, propertyName);
                entitySettings.AuditableProperties.Add(propertySettings);
            }
            propertySettings.ValueMapper = valueMapper;
            propertySettings.ColumnNameAlias = columnAlias;
        }

        /// <summary>
        /// Remove table model from Audit.
        /// </summary>
        internal void Remove(Type type)
        {
            if (!_entitiesSettings.Remove(type))
            {
                throw new InvalidOperationException($"The table {type.Name} is not yet configured for audit.");
            }

            if (_entitiesSettings.Count == 0)
            {
                throw new InvalidOperationException("No tables left for auditing.");
            }
        }

        internal void Remove(Type type, List<string> propertiesNames)
        {
            if (!_entitiesSettings.TryGetValue(type, out var entitySettings))
            {
                throw new InvalidOperationException($"The table {type.Name} is not yet configured for audit.");
            }

            foreach (var propertyName in propertiesNames)
            {
                var propertySettings = Get(entitySettings, propertyName)
                    ?? throw new InvalidOperationException($"The property {propertyName} is not yet set to be audited.");

                entitySettings.AuditableProperties.Remove(propertySettings);
            }

            if (entitySettings.AuditableProperties.Count == 0)
            {
                throw new InvalidOperationException($"No columns left to audit in {type.Name} table model.");
            }
        }

        #endregion

        #region Public

        public ReadOnlyCollection<EntityAuditSettings> EntityAuditSettings
            => new(_entitiesSettings.Values.ToList());

        public Type? AuditTrailTableModelType { get; internal set; }

        public Func<RowAuditInfo, object?, CancellationToken, Task<object?>>? AuditMappingCallBackAsync { get; internal set; }

        #endregion
    }
}
