﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Network_Monitor.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "15.5.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool MustUpgrade {
            get {
                return ((bool)(this["MustUpgrade"]));
            }
            set {
                this["MustUpgrade"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("8.8.8.8")]
        public string PingHost {
            get {
                return ((string)(this["PingHost"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("00:00:01")]
        public global::System.TimeSpan Interval {
            get {
                return ((global::System.TimeSpan)(this["Interval"]));
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool Topmost {
            get {
                return ((bool)(this["Topmost"]));
            }
            set {
                this["Topmost"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool ShowInTaskbar {
            get {
                return ((bool)(this["ShowInTaskbar"]));
            }
            set {
                this["ShowInTaskbar"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("96")]
        public int Size {
            get {
                return ((int)(this["Size"]));
            }
            set {
                this["Size"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute(@"
          <WindowPlacement xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
            <Length>0</Length>
            <Flags />
            <ShowCommand>Hide</ShowCommand>
            <MinimizedPosition>
              <X>0</X>
              <Y>0</Y>
            </MinimizedPosition>
            <MaximizedPosition>
              <X>0</X>
              <Y>0</Y>
            </MaximizedPosition>
            <NormalBounds>
              <Left>0</Left>
              <Top>0</Top>
              <Right>0</Right>
              <Bottom>0</Bottom>
            </NormalBounds>
          </WindowPlacement>
        ")]
        public global::Network_Monitor.Placement.WindowPlacement Placement {
            get {
                return ((global::Network_Monitor.Placement.WindowPlacement)(this["Placement"]));
            }
            set {
                this["Placement"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool Bits {
            get {
                return ((bool)(this["Bits"]));
            }
            set {
                this["Bits"] = value;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("00:00:04")]
        public global::System.TimeSpan Timeout {
            get {
                return ((global::System.TimeSpan)(this["Timeout"]));
            }
        }
    }
}
