// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

/* This file provides a basefunctionallity for IVsCfgProvider2.
   Instead of using the IVsProjectCfgEventsHelper object we have our own little sink and call our own helper methods
   similiar to the interface. But there is no real benefit in inheriting from the interface in the first place. 
   Using the helper object seems to be:  
    a) undocumented
    b) not really wise in the managed world
*/

namespace Microsoft.VisualStudioTools.Project
{
    [ComVisible(true)]
    internal abstract class ConfigProvider : IVsCfgProvider2
    {
        internal const string configString = " '$(Configuration)' == '{0}' ";
        internal const string configPlatformString = " '$(Configuration)|$(Platform)' == '{0}|{1}' ";
        internal const string AnyCPUPlatform = "Any CPU";
        internal const string x86Platform = "x86";
        internal const string x64Platform = "x64";
        internal const string ARMPlatform = "ARM";

        private EventSinkCollection cfgEventSinks = new EventSinkCollection();
        private Dictionary<string, ProjectConfig> configurationsList = new Dictionary<string, ProjectConfig>();

        public ConfigProvider(ProjectNode manager)
        {
            this.ProjectMgr = manager;
        }

        /// <summary>
        /// The associated project.
        /// </summary>
        internal ProjectNode ProjectMgr { get; }

        /// <summary>
        /// If the project system wants to add custom properties to the property group then 
        /// they provide us with this data.
        /// Returns/sets the [(<propName, propCondition>) <propValue>] collection
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual List<KeyValuePair<KeyValuePair<string, string>, string>> NewConfigProperties { get; set; } = new List<KeyValuePair<KeyValuePair<string, string>, string>>();

        /// <summary>
        /// Creates new Project Configuartion objects based on the configuration name.
        /// </summary>
        /// <param name="configName">The name of the configuration</param>
        /// <returns>An instance of a ProjectConfig object.</returns>
        protected ProjectConfig GetProjectConfiguration(string configName)
        {
            // if we already created it, return the cached one
            if (this.configurationsList.ContainsKey(configName))
            {
                return this.configurationsList[configName];
            }

            var requestedConfiguration = CreateProjectConfiguration(configName);
            this.configurationsList.Add(configName, requestedConfiguration);

            return requestedConfiguration;
        }

        protected abstract ProjectConfig CreateProjectConfiguration(string configName);

        #region IVsCfgProvider2 methods

        /// <summary>
        /// Copies an existing configuration name or creates a new one. 
        /// </summary>
        /// <param name="name">The name of the new configuration.</param>
        /// <param name="cloneName">the name of the configuration to copy, or a null reference, indicating that AddCfgsOfCfgName should create a new configuration.</param>
        /// <param name="fPrivate">Flag indicating whether or not the new configuration is private. If fPrivate is set to true, the configuration is private. If set to false, the configuration is public. This flag can be ignored.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int AddCfgsOfCfgName(string name, string cloneName, int fPrivate)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            // First create the condition that represent the configuration we want to clone
            var condition = (cloneName == null ? string.Empty : string.Format(CultureInfo.InvariantCulture, configString, cloneName).Trim());

            // Get all configs
            var configGroup = new List<ProjectPropertyGroupElement>(this.ProjectMgr.BuildProject.Xml.PropertyGroups);
            ProjectPropertyGroupElement configToClone = null;

            if (cloneName != null)
            {
                // Find the configuration to clone
                foreach (var currentConfig in configGroup)
                {
                    // Only care about conditional property groups
                    if (currentConfig.Condition == null || currentConfig.Condition.Length == 0)
                    {
                        continue;
                    }

                    // Skip if it isn't the group we want
                    if (StringComparer.OrdinalIgnoreCase.Equals(currentConfig.Condition.Trim(), condition))
                    {
                        continue;
                    }

                    configToClone = currentConfig;
                }
            }

            ProjectPropertyGroupElement newConfig = null;
            if (configToClone != null)
            {
                // Clone the configuration settings
                newConfig = this.ProjectMgr.ClonePropertyGroup(configToClone);
                //Will be added later with the new values to the path

                foreach (var property in newConfig.Properties)
                {
                    if (property.Name.Equals("OutputPath", StringComparison.OrdinalIgnoreCase))
                    {
                        property.Parent.RemoveChild(property);
                    }
                }
            }
            else
            {
                // no source to clone from, lets just create a new empty config
                newConfig = this.ProjectMgr.BuildProject.Xml.AddPropertyGroup();
                // Get the list of property name, condition value from the config provider
                var propVals = this.NewConfigProperties;
                foreach (var data in propVals)
                {
                    var propData = data.Key;
                    var value = data.Value;
                    var newProperty = newConfig.AddProperty(propData.Key, value);
                    if (!string.IsNullOrEmpty(propData.Value))
                    {
                        newProperty.Condition = propData.Value;
                    }
                }
            }

            //add the output path
            var outputBasePath = this.ProjectMgr.OutputBaseRelativePath;
            newConfig.AddProperty("OutputPath", CommonUtils.NormalizeDirectoryPath(Path.Combine(outputBasePath, name)));

            // Set the condition that will define the new configuration
            var newCondition = string.Format(CultureInfo.InvariantCulture, configString, name);
            newConfig.Condition = newCondition;

            NotifyOnCfgNameAdded(name);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Copies an existing platform name or creates a new one. 
        /// </summary>
        /// <param name="platformName">The name of the new platform.</param>
        /// <param name="clonePlatformName">The name of the platform to copy, or a null reference, indicating that AddCfgsOfPlatformName should create a new platform.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int AddCfgsOfPlatformName(string platformName, string clonePlatformName)
        {
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Deletes a specified configuration name. 
        /// </summary>
        /// <param name="name">The name of the configuration to be deleted.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int DeleteCfgsOfCfgName(string name)
        {
            // We need to QE/QS the project file
            if (!this.ProjectMgr.QueryEditProjectFile(false))
            {
                throw Marshal.GetExceptionForHR(VSConstants.OLE_E_PROMPTSAVECANCELLED);
            }

            if (name == null)
            {
                // The configuration " '$(Configuration)' ==  " does not exist, so technically the goal
                // is achieved so return S_OK
                return VSConstants.S_OK;
            }
            // Verify that this config exist
            var configs = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);
            foreach (var config in configs)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(config, name))
                {
                    // Create condition of config to remove
                    var condition = string.Format(CultureInfo.InvariantCulture, configString, config);

                    foreach (var element in this.ProjectMgr.BuildProject.Xml.PropertyGroups)
                    {
                        if (StringComparer.OrdinalIgnoreCase.Equals(element.Condition, condition))
                        {
                            element.Parent.RemoveChild(element);
                        }
                    }

                    NotifyOnCfgNameDeleted(name);
                }
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Deletes a specified platform name. 
        /// </summary>
        /// <param name="platName">The platform name to delet.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int DeleteCfgsOfPlatformName(string platName)
        {
            return VSConstants.E_NOTIMPL;
        }

        /// <summary>
        /// Returns the existing configurations stored in the project file.
        /// </summary>
        /// <param name="celt">Specifies the requested number of property names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of configuration property names specified by celt. This parameter can also be a null reference if the celt parameter is zero. 
        /// On output, names contains configuration property names.</param>
        /// <param name="actual">The actual number of property names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgNames(uint celt, string[] names, uint[] actual)
        {
            // get's called twice, once for allocation, then for retrieval            
            var i = 0;

            var configList = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);

            if (names != null)
            {
                foreach (var config in configList)
                {
                    names[i++] = config;
                    if (i == celt)
                    {
                        break;
                    }
                }
            }
            else
            {
                i = configList.Length;
            }

            if (actual != null)
            {
                actual[0] = (uint)i;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns the configuration associated with a specified configuration or platform name. 
        /// </summary>
        /// <param name="name">The name of the configuration to be returned.</param>
        /// <param name="platName">The name of the platform for the configuration to be returned.</param>
        /// <param name="cfg">The implementation of the IVsCfg interface.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgOfName(string name, string platName, out IVsCfg cfg)
        {
            cfg = null;
            cfg = this.GetProjectConfiguration(name);

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns a specified configuration property. 
        /// </summary>
        /// <param name="propid">Specifies the property identifier for the property to return. For valid propid values, see __VSCFGPROPID.</param>
        /// <param name="var">The value of the property.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgProviderProperty(int propid, out object var)
        {
            var = false;
            switch ((__VSCFGPROPID)propid)
            {
                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgAdd:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgDelete:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsCfgRename:
                    var = true;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformAdd:
                    var = false;
                    break;

                case __VSCFGPROPID.VSCFGPROPID_SupportsPlatformDelete:
                    var = false;
                    break;
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns the per-configuration objects for this object. 
        /// </summary>
        /// <param name="celt">Number of configuration objects to be returned or zero, indicating a request for an unknown number of objects.</param>
        /// <param name="a">On input, pointer to an interface array or a null reference. On output, this parameter points to an array of IVsCfg interfaces belonging to the requested configuration objects.</param>
        /// <param name="actual">The number of configuration objects actually returned or a null reference, if this information is not necessary.</param>
        /// <param name="flags">Flags that specify settings for project configurations, or a null reference (Nothing in Visual Basic) if no additional flag settings are required. For valid prgrFlags values, see __VSCFGFLAGS.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetCfgs(uint celt, IVsCfg[] a, uint[] actual, uint[] flags)
        {
            if (flags != null)
            {
                flags[0] = 0;
            }

            var i = 0;
            var configList = GetPropertiesConditionedOn(ProjectFileConstants.Configuration);

            if (a != null)
            {
                foreach (var configName in configList)
                {
                    a[i] = this.GetProjectConfiguration(configName);

                    i++;
                    if (i == celt)
                    {
                        break;
                    }
                }
            }
            else
            {
                i = configList.Length;
            }

            if (actual != null)
            {
                actual[0] = (uint)i;
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Returns one or more platform names. 
        /// </summary>
        /// <param name="celt">Specifies the requested number of platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of platform names specified by celt. This parameter can also be a null reference if the celt parameter is zero. On output, names contains platform names.</param>
        /// <param name="actual">The actual number of platform names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetPlatformNames(uint celt, string[] names, uint[] actual)
        {
            var platforms = this.GetPlatformsFromProject();
            return GetPlatforms(celt, names, actual, platforms);
        }

        /// <summary>
        /// Returns the set of platforms that are installed on the user's machine. 
        /// </summary>
        /// <param name="celt">Specifies the requested number of supported platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of names specified by celt. This parameter can also be a null reference (Nothing in Visual Basic)if the celt parameter is zero. On output, names contains the names of supported platforms</param>
        /// <param name="actual">The actual number of platform names returned.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int GetSupportedPlatformNames(uint celt, string[] names, uint[] actual)
        {
            var platforms = this.GetSupportedPlatformsFromProject();
            return GetPlatforms(celt, names, actual, platforms);
        }

        /// <summary>
        /// Assigns a new name to a configuration. 
        /// </summary>
        /// <param name="old">The old name of the target configuration.</param>
        /// <param name="newname">The new name of the target configuration.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int RenameCfgsOfCfgName(string old, string newname)
        {
            // First create the condition that represent the configuration we want to rename
            var condition = string.Format(CultureInfo.InvariantCulture, configString, old).Trim();

            foreach (var config in this.ProjectMgr.BuildProject.Xml.PropertyGroups)
            {
                // Only care about conditional property groups
                if (config.Condition == null || config.Condition.Length == 0)
                {
                    continue;
                }

                // Skip if it isn't the group we want
                if (StringComparer.OrdinalIgnoreCase.Equals(config.Condition.Trim(), condition))
                {
                    continue;
                }

                // Change the name 
                config.Condition = string.Format(CultureInfo.InvariantCulture, configString, newname);
                // Update the name in our config list
                if (this.configurationsList.ContainsKey(old))
                {
                    var configuration = this.configurationsList[old];
                    this.configurationsList.Remove(old);
                    this.configurationsList.Add(newname, configuration);
                    // notify the configuration of its new name
                    configuration.ConfigName = newname;
                }

                NotifyOnCfgNameRenamed(old, newname);
            }

            return VSConstants.S_OK;
        }

        /// <summary>
        /// Cancels a registration for configuration event notification. 
        /// </summary>
        /// <param name="cookie">The cookie used for registration.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int UnadviseCfgProviderEvents(uint cookie)
        {
            this.cfgEventSinks.RemoveAt(cookie);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Registers the caller for configuration event notification. 
        /// </summary>
        /// <param name="sink">Reference to the IVsCfgProviderEvents interface to be called to provide notification of configuration events.</param>
        /// <param name="cookie">Reference to a token representing the completed registration</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code.</returns>
        public virtual int AdviseCfgProviderEvents(IVsCfgProviderEvents sink, out uint cookie)
        {
            cookie = this.cfgEventSinks.Add(sink);
            return VSConstants.S_OK;
        }

        #endregion

        #region helper methods
        /// <summary>
        /// Called when a new configuration name was added.
        /// </summary>
        /// <param name="name">The name of configuration just added.</param>
        private void NotifyOnCfgNameAdded(string name)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameAdded(name));
            }
        }

        /// <summary>
        /// Called when a config name was deleted.
        /// </summary>
        /// <param name="name">The name of the configuration.</param>
        private void NotifyOnCfgNameDeleted(string name)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameDeleted(name));
            }
        }

        /// <summary>
        /// Called when a config name was renamed
        /// </summary>
        /// <param name="oldName">Old configuration name</param>
        /// <param name="newName">New configuration name</param>
        private void NotifyOnCfgNameRenamed(string oldName, string newName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnCfgNameRenamed(oldName, newName));
            }
        }

        /// <summary>
        /// Called when a platform name was added
        /// </summary>
        /// <param name="platformName">The name of the platform.</param>
        private void NotifyOnPlatformNameAdded(string platformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnPlatformNameAdded(platformName));
            }
        }

        /// <summary>
        /// Called when a platform name was deleted
        /// </summary>
        /// <param name="platformName">The name of the platform.</param>
        private void NotifyOnPlatformNameDeleted(string platformName)
        {
            foreach (IVsCfgProviderEvents sink in this.cfgEventSinks)
            {
                ErrorHandler.ThrowOnFailure(sink.OnPlatformNameDeleted(platformName));
            }
        }

        /// <summary>
        /// Gets all the platforms defined in the project
        /// </summary>
        /// <returns>An array of platform names.</returns>
        private string[] GetPlatformsFromProject()
        {
            var platforms = GetPropertiesConditionedOn(ProjectFileConstants.Platform);

            if (platforms == null || platforms.Length == 0)
            {
                return new[] { x86Platform, AnyCPUPlatform, x64Platform, ARMPlatform };
            }

            for (var i = 0; i < platforms.Length; i++)
            {
                platforms[i] = ConvertPlatformToVsProject(platforms[i]);
            }

            return platforms;
        }

        /// <summary>
        /// Return the supported platform names.
        /// </summary>
        /// <returns>An array of supported platform names.</returns>
        internal string[] GetSupportedPlatformsFromProject()
        {
            var platforms = this.ProjectMgr.BuildProject.GetPropertyValue(ProjectFileConstants.AvailablePlatforms);

            if (platforms == null)
            {
                return Array.Empty<string>();
            }

            if (platforms.Contains(","))
            {
                return platforms.Split(',');
            }

            return new[] { platforms };
        }

        /// <summary>
        /// Helper function to convert AnyCPU to Any CPU.
        /// </summary>
        /// <param name="oldName">The oldname.</param>
        /// <returns>The new name.</returns>
        private static string ConvertPlatformToVsProject(string oldPlatformName)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(oldPlatformName, ProjectFileValues.AnyCPU))
            {
                return AnyCPUPlatform;
            }

            return oldPlatformName;
        }

        /// <summary>
        /// Common method for handling platform names.
        /// </summary>
        /// <param name="celt">Specifies the requested number of platform names. If this number is unknown, celt can be zero.</param>
        /// <param name="names">On input, an allocated array to hold the number of platform names specified by celt. This parameter can also be null if the celt parameter is zero. On output, names contains platform names</param>
        /// <param name="actual">A count of the actual number of platform names returned.</param>
        /// <param name="platforms">An array of available platform names</param>
        /// <returns>A count of the actual number of platform names returned.</returns>
        /// <devremark>The platforms array is never null. It is assured by the callers.</devremark>
        internal static int GetPlatforms(uint celt, string[] names, uint[] actual, string[] platforms)
        {
            Utilities.ArgumentNotNull("platforms", platforms);
            if (names == null)
            {
                if (actual == null || actual.Length == 0)
                {
                    throw new ArgumentException(SR.GetString(SR.InvalidParameter), "actual");
                }

                actual[0] = (uint)platforms.Length;
                return VSConstants.S_OK;
            }

            //Degenarate case
            if (celt == 0)
            {
                if (actual != null && actual.Length != 0)
                {
                    actual[0] = (uint)platforms.Length;
                }

                return VSConstants.S_OK;
            }

            uint returned = 0;
            for (var i = 0; i < platforms.Length && names.Length > returned; i++)
            {
                names[returned] = platforms[i];
                returned++;
            }

            if (actual != null && actual.Length != 0)
            {
                actual[0] = returned;
            }

            if (celt > returned)
            {
                return VSConstants.S_FALSE;
            }

            return VSConstants.S_OK;
        }
        #endregion

        /// <summary>
        /// Get all the configurations in the project.
        /// </summary>
        internal string[] GetPropertiesConditionedOn(string constant)
        {
            this.ProjectMgr.BuildProject.ReevaluateIfNecessary();
            this.ProjectMgr.BuildProject.ConditionedProperties.TryGetValue(constant, out var configurations);

            return (configurations == null) ? Array.Empty<string>() : configurations.ToArray();
        }
    }
}

