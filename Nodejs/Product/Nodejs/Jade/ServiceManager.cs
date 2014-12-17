﻿//*********************************************************//
//    Copyright (c) Microsoft. All rights reserved.
//    
//    Apache 2.0 License
//    
//    You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//    
//    Unless required by applicable law or agreed to in writing, software 
//    distributed under the License is distributed on an "AS IS" BASIS, 
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or 
//    implied. See the License for the specific language governing 
//    permissions and limitations under the License.
//
//*********************************************************//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.NodejsTools.Jade {
    sealed class ServiceManager : IDisposable {
        private const string ServiceManagerId = "ServiceManager";
        private IPropertyOwner _propertyOwner;
        private object _lock = new object();

        private Dictionary<Type, object> _servicesByType = new Dictionary<Type, object>();
        private Dictionary<Guid, object> _servicesByGuid = new Dictionary<Guid, object>();
        private Dictionary<Tuple<Type, string>, object> _servicesByContentType = new Dictionary<Tuple<Type, string>, object>();

        private ServiceManager(IPropertyOwner propertyOwner) {
            _propertyOwner = propertyOwner;
            _propertyOwner.Properties.AddProperty(ServiceManagerId, this);
        }

        /// <summary>
        /// Returns service manager attached to a given Property owner
        /// </summary>
        /// <param name="propertyOwner">Property owner</param>
        /// <returns>Service manager instance</returns>
        public static ServiceManager FromPropertyOwner(IPropertyOwner propertyOwner) {
            ServiceManager sm = null;

            if (propertyOwner.Properties.ContainsProperty(ServiceManagerId)) {
                sm = propertyOwner.Properties.GetProperty(ServiceManagerId) as ServiceManager;
                return sm;
            }

            return new ServiceManager(propertyOwner);
        }

        /// <summary>
        /// Retrieves service from a service manager for this Property owner given service type
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="propertyOwner">Property owner</param>
        /// <returns>Service instance</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static T GetService<T>(IPropertyOwner propertyOwner) where T : class {
            try {
                var sm = ServiceManager.FromPropertyOwner(propertyOwner);
                Debug.Assert(sm != null);

                return sm.GetService<T>();
            } catch (Exception) {
                return null;
            }
        }

        /// <summary>
        /// Retrieves service from a service manager for this property owner given service type GUID.
        /// Primarily used to retrieve services that implement COM interop and are usable from native code.
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="propertyOwner">Property owner</param>
        /// <returns>Service instance</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static object GetService(IPropertyOwner propertyOwner, ref Guid serviceGuid) {
            try {
                var sm = ServiceManager.FromPropertyOwner(propertyOwner);
                Debug.Assert(sm != null);

                return sm.GetService(ref serviceGuid);
            } catch (Exception) {
                return null;
            }
        }

        /// <summary>
        ///  Retrieves service from a service manager for this Property owner given service type and content type
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="propertyOwner">Property owner</param>
        /// <param name="contentType">Content type</param>
        /// <returns>Service instance</returns>
        public static T GetService<T>(IPropertyOwner propertyOwner, IContentType contentType) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            if (sm != null)
                return sm.GetService<T>(contentType);

            return null;
        }

        public static ICollection<T> GetAllServices<T>(IPropertyOwner propertyOwner) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            if (sm != null)
                return sm.GetAllServices<T>();

            return new List<T>();
        }

        /// <summary>
        /// Add service to a service manager associated with a particular Property owner
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="serviceInstance">Service instance</param>
        /// <param name="propertyOwner">Property owner</param>
        public static void AddService<T>(T serviceInstance, IPropertyOwner propertyOwner) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.AddService<T>(serviceInstance);
        }

        /// <summary>
        /// Add content type specific service to a service manager associated with a particular Property owner
        /// </summary>
        /// <typeparam name="T">Service type</typeparam>
        /// <param name="serviceInstance">Service instance</param>
        /// <param name="propertyOwner">Property owner</param>
        /// <param name="contentType">Content type of the service</param>
        public static void AddService<T>(T serviceInstance, IPropertyOwner propertyOwner, IContentType contentType) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.AddService<T>(serviceInstance, contentType);
        }

        /// <summary>
        /// Add service to a service manager associated with a particular property owner.
        /// Typically used to store services implemented in native code and identified by
        /// the interface GUID.
        /// </summary>
        /// <typeparam name="serviceGuid">Service GUID</typeparam>
        /// <param name="serviceInstance">Service instance</param>
        /// <param name="propertyOwner">Property owner</param>
        public static void AddService(ref Guid serviceGuid, object serviceInstance, IPropertyOwner propertyOwner) {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.AddService(ref serviceGuid, serviceInstance);
        }

        public static void RemoveService<T>(IPropertyOwner propertyOwner) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.RemoveService<T>();
        }

        public static void RemoveService<T>(IPropertyOwner propertyOwner, IContentType contentType) where T : class {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.RemoveService<T>(contentType);
        }

        public static void RemoveService(IPropertyOwner propertyOwner, ref Guid guidService) {
            var sm = ServiceManager.FromPropertyOwner(propertyOwner);
            Debug.Assert(sm != null);

            sm.RemoveService(ref guidService);
        }

        private T GetService<T>() where T : class {
            lock (_lock) {
                object service = null;

                if (!_servicesByType.TryGetValue(typeof(T), out service)) {
                    // try walk through and cast. Perhaps someone is asking for IFoo
                    // that is implemented on class Bar but Bar was added as Bar, not as IFoo
                    foreach (var kvp in _servicesByType) {
                        service = kvp.Value as T;
                        if (service != null)
                            break;
                    }
                }

                return service as T;
            }
        }

        private T GetService<T>(IContentType contentType) where T : class {
            lock (_lock) {
                object service = null;

                _servicesByContentType.TryGetValue(Tuple.Create(typeof(T), contentType.TypeName), out service);
                if (service != null)
                    return service as T;

                // Try walking through and cast. Perhaps someone is asking for IFoo
                // that is implemented on class Bar but Bar was added as Bar, not as IFoo
                foreach (var kvp in _servicesByContentType) {
                    if (String.Compare(kvp.Key.Item2, contentType.TypeName, StringComparison.OrdinalIgnoreCase) == 0) {
                        service = kvp.Value as T;
                        if (service != null)
                            return service as T;
                    }
                }

                // iterate through base types since Razor, PHP and ASP.NET content type derive from HTML
                foreach (var ct in contentType.BaseTypes) {
                    service = GetService<T>(ct);
                    if (service != null)
                        break;
                }

                return service as T;
            }
        }

        private object GetService(ref Guid serviceGuid) {
            lock (_lock) {
                foreach (var kvp in _servicesByGuid) {
                    if (serviceGuid.Equals(kvp.Key))
                        return kvp.Value;
                }

                foreach (var kvp in _servicesByType) {
                    if (serviceGuid.Equals(kvp.Value.GetType().GUID))
                        return kvp.Value;
                }

                return null;
            }
        }

        private ICollection<T> GetAllServices<T>() where T : class {
            var list = new List<T>();

            lock (_lock) {
                foreach (var kvp in _servicesByType) {
                    var service = kvp.Value as T;
                    if (service != null)
                        list.Add(service);
                }
            }

            return list;
        }

        private void AddService<T>(T serviceInstance) where T : class {
            lock (_lock) {
                if (GetService<T>() == null) {
                    _servicesByType.Add(typeof(T), serviceInstance);
                }
            }
        }

        private void AddService<T>(T serviceInstance, IContentType contentType) where T : class {
            lock (_lock) {
                if (GetService<T>(contentType) == null) {
                    _servicesByContentType.Add(Tuple.Create(typeof(T), contentType.TypeName), serviceInstance);
                }
            }
        }

        private void AddService(ref Guid serviceGuid, object serviceInstance) {
            lock (_lock) {
                if (GetService(ref serviceGuid) == null)
                    _servicesByGuid.Add(serviceGuid, serviceInstance);
            }
        }

        private void RemoveService<T>() where T : class {
            _servicesByType.Remove(typeof(T));
        }

        private void RemoveService<T>(IContentType contentType) where T : class {
            lock (_lock) {
                _servicesByContentType.Remove(Tuple.Create(typeof(T), contentType.TypeName));
            }
        }

        private void RemoveService(ref Guid guidService) {
            _servicesByGuid.Remove(guidService);
        }

        public void Dispose() {
            if (_propertyOwner != null) {
                _propertyOwner.Properties.RemoveProperty(ServiceManagerId);

#if NOT
                var properties = _propertyOwner.Properties.PropertyList;
                var sb = new StringBuilder();

                foreach (var prop in properties)
                {
                    sb.Append(prop.Value.GetType().ToString());
                    sb.Append("\r\n");
                }

                Debug.Assert(properties.Count == 0, String.Format("There are still {0} services attached to the buffer:\r\n {1}", properties.Count, sb.ToString()));
#endif

                _servicesByGuid.Clear();
                _servicesByType.Clear();
                _servicesByContentType.Clear();

                _propertyOwner = null;
            }
        }
    }
}
