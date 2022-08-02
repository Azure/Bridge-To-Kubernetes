// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Provides functionality/signatures common to all dependency-injectable services
    /// </summary>
    internal abstract class ServiceBase : IDisposable
    {
        protected bool _baseDisposed = false;

        protected ServiceBase()
        { }

        protected ServiceBase(bool autoDisposeEnabled)
        {
            this.AutoDispose = autoDisposeEnabled;
        }

        /// <summary>
        /// Fired when this instance is starting to Dispose
        /// </summary>
        public event EventHandler Disposing;

        /// <summary>
        /// Fired when this instance has finished Dispose
        /// </summary>
        public event EventHandler Disposed;

        /// <summary>
        /// Whether the provided auto-dispose functionality should be used
        /// </summary>
        protected bool AutoDispose { get; set; } = true;

        /// <summary>
        /// Initializes the service instance
        /// </summary>
        /// <returns></returns>
        public virtual Task InitializeAsync()
        {
            return Task.FromResult(0);
        }

        /// <summary>
        /// Provides a reflection-based dispose operation that automatically disposes of all IDisposable fields
        /// </summary>
        public virtual void Dispose()
        {
            if (this._baseDisposed)
            {
                return;
            }

            this._baseDisposed = true;

            this.InvokeDisposing();
            if (this.AutoDispose)
            {
                this._AutoDispose();
            }
            this.InvokeDisposed();
        }

        /// <summary>
        /// Invokes the <see cref="Disposing"/> event
        /// </summary>
        protected void InvokeDisposing()
        {
            this.Disposing?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Invokes the <see cref="Disposed"/> event
        /// </summary>
        protected void InvokeDisposed()
        {
            this.Disposed?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Use reflection to auto dispose
        /// </summary>
        private void _AutoDispose()
        {
            /* ---- Dispose fields ---- */
            var nonNullInstanceFields = this.GetType().GetRuntimeFields()
                .Where(f => !f.IsStatic)
                .Where(f => f.GetValue(this) != null);
            // Instances of ServiceBase or IService are managed by Autofac. Don't dispose them in case they are singleton.
            var ownedInstanceFields = nonNullInstanceFields
                .Where(f =>
                {
                    var val = f.GetValue(this);
                    return !(val is ServiceBase) && !(val is IService);
                });
            var fieldsToDispose = ownedInstanceFields
                .Where(f => f.GetValue(this) is IDisposable);
            foreach (var field in fieldsToDispose)
            {
                try
                {
                    var disposableObj = field.GetValue(this) as IDisposable;
                    disposableObj?.Dispose();
                    field.SetValue(this, null);
                }
                catch { }
            }

            /* ---- Dispose properties ---- */
            var instanceProperties = this.GetType().GetRuntimeProperties().Where(p => !p.GetMethod.IsStatic);
            // Instances of ServiceBase or IService are managed by Autofac. Don't dispose them in case they are singleton.
            var nonNullOwnedInstanceProperties = instanceProperties.Where(p =>
            {
                try
                {
                    var val = p.GetValue(this);
                    return val != null && !(val is ServiceBase) && !(val is IService);
                }
                catch (Exception)
                {
                    // Property getter may try to access fields that have already been disposed/nulled, or NotImplemented properties.
                    // In any case if we can't access the property we should not dispose it.
                    return false;
                }
            });
            var propertiesToDispose = nonNullOwnedInstanceProperties.Where(p => p.GetValue(this) is IDisposable);
            foreach (var prop in propertiesToDispose)
            {
                try
                {
                    var disposableObj = prop.GetValue(this) as IDisposable;
                    disposableObj?.Dispose();
                    prop.SetValue(this, null);
                }
                catch { }
            }
        }
    }
}