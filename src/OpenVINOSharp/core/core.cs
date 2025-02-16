﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;


namespace OpenVinoSharp
{

    /// <summary>
    /// <para>This class represents an OpenVINO runtime Core entity.</para>
    /// <ingroupe>ov_runtime_c#_api</ingroupe>
    /// </summary>
    /// <remarks>User applications can create several Core class instances, but in this case the underlying plugins
    /// are created multiple times and not shared between several Core instances.The recommended way is to have
    /// a single Core instance per application.
    /// </remarks>
    public class Core
    {
        /// <summary>
        /// [private]Core class pointer.
        /// </summary>
        private IntPtr m_ptr = IntPtr.Zero;
        /// <summary>
        /// [public]Core class pointer.
        /// </summary>
        public IntPtr Ptr { get { return m_ptr; } set { m_ptr = value; } }

        /// <summary>
        /// Represent all available devices.
        /// </summary>
        struct ov_available_devices_t
        {
            /// <summary>
            ///  devices' name
            /// </summary>
            public IntPtr devices;
            /// <summary>
            /// devices' number
            /// </summary>
            public ulong size;
        }

        string[] labelNames = null;
        /// <summary>
        /// 返回标签名字数组
        /// </summary>
        public string[] GetLabelNames()
        {
            return labelNames;
        }


        /// <summary>
        ///  Constructs an OpenVINO Core instance with devices and their plugins description.
        ///     <para>There are two ways how to configure device plugins:</para>
        ///     <para>1. (default) Use XML configuration file in case of dynamic libraries build;</para>
        ///     <para>2. Use strictly defined configuration in case of static libraries build.</para>
        /// </summary>
        /// <param name="xml_config_file">
        ///  Path to the .xml file with plugins to load from. If the XML configuration file is not
        ///  specified, default OpenVINO Runtime plugins are loaded from:
        ///     <para>1. (dynamic build) default `plugins.xml` file located in the same folder as OpenVINO runtime shared library;</para>
        ///     <para>2. (static build) statically defined configuration.In this case path to the.xml file is ignored.</para>
        ///  </param>
        public Core(string xml_config_file = null) 
        {
            ExceptionStatus status;
            if (!String.IsNullOrEmpty(xml_config_file)) {
                status = NativeMethods.ov_core_create_with_config(xml_config_file, ref m_ptr);
            }
            status = NativeMethods.ov_core_create(ref m_ptr);
            if (status != 0) {
                m_ptr = IntPtr.Zero;

                System.Diagnostics.Debug.WriteLine("Core init error : " + status.ToString());
            }
            
        }
        /// <summary>
        /// Core's destructor
        /// </summary>
        ~Core() { dispose(); }
        /// <summary>
        /// Release unmanaged resources
        /// </summary>
        public void dispose()
        {
            if (m_ptr == IntPtr.Zero)
            {
                return;
            }
            NativeMethods.ov_core_free(m_ptr);
    
            m_ptr = IntPtr.Zero;
        }
        /// <summary>
        /// Returns device plugins version information.
        /// </summary>
        /// <param name="device_name">Device name to identify a plugin.</param>
        /// <returns>A vector of versions.</returns>
        /// <remarks>
        /// Device name can be complex and identify multiple devices at once like `HETERO:CPU,GPU`;
        /// in this case, std::map contains multiple entries, each per device.
        /// </remarks>
        public KeyValuePair<string, Version> get_versions(string device_name)
        {
            ExceptionStatus status;
            int l = Marshal.SizeOf(typeof(CoreVersionList));
            IntPtr ptr_core_version_s = Marshal.AllocHGlobal(l);
            sbyte[] c_device_name = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(device_name));
            status = NativeMethods.ov_core_get_versions_by_device_name(m_ptr, ref c_device_name[0], ptr_core_version_s);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core get_versions() error : " + status.ToString());
                return new KeyValuePair<string, Version>();
            }
            var temp1 = Marshal.PtrToStructure(ptr_core_version_s, typeof(CoreVersionList));

            CoreVersionList core_version_s = (CoreVersionList)temp1;
            var temp2 = Marshal.PtrToStructure(core_version_s.core_version, typeof(CoreVersion));
            CoreVersion core_version = (CoreVersion)temp2;
            KeyValuePair<string, Version> value = new KeyValuePair<string, Version>(core_version.device_name, core_version.version);
            NativeMethods.ov_core_versions_free(ptr_core_version_s);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core get_versions() error : " + status.ToString());
                return new KeyValuePair<string, Version>();
            }
            return value;
        }


        /// <summary>
        /// Reads models from IR / ONNX / PDPD / TF / TFLite file formats.
        /// </summary>
        /// <param name="model_path">Path to a model.</param>
        /// <param name="bin_path">Path to a data file.</param>
        /// <returns>A model.</returns>
        /// <remarks>
        ///     <para>
        ///     For IR format (*.bin):
        ///     if `bin_path` is empty, will try to read a bin file with the same name as xml and
        ///     if the bin file with the same name is not found, will load IR without weights.
        ///     For the following file formats the `bin_path` parameter is not used:
        ///     </para>
        ///     <para>ONNX format (*.onnx)</para>
        ///     <para>PDPD(*.pdmodel)</para>
        ///     <para>TF(*.pb)</para>
        ///     <para>TFLite(*.tflite)</para>
        /// </remarks>
        public Model read_model(string model_path, string bin_path = "") 
        {
            readLabelNames(model_path);
            IntPtr model_ptr = new IntPtr();
            sbyte[] c_model_path = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(model_path));
            ExceptionStatus status;
            if (bin_path == "") {
                sbyte c_bin_path = new sbyte();
                status = NativeMethods.ov_core_read_model(m_ptr, ref c_model_path[0], ref c_bin_path, ref model_ptr);
            } 
            else
            {
                sbyte[] c_bin_path = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(bin_path));
                status = NativeMethods.ov_core_read_model(m_ptr, ref c_model_path[0], ref c_bin_path[0], ref model_ptr);
            }
            
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core read_model() error : " + status.ToString());
                return new Model(IntPtr.Zero);
            }
            return new Model(model_ptr);
        }


        private void readLabelNames(string model_path)
        {
            try
            {

                string extName = System.IO.Path.GetExtension(model_path);

                string tempnames = null;
                switch(extName)
                {
                    case ".xml":
                        XElement xe = XElement.Load(model_path);
                        tempnames = xe.Elements("rt_info").First().Elements("framework").First().Elements("names").First().Attribute("value").Value;
                        break;
                    case ".onnx":
                        var data = File.OpenRead(model_path);
                        Onnx.ModelProto model = Onnx.ModelProto.Parser.ParseFrom(data);
                        var dict = model.MetadataProps.ToDictionary(kv => kv.Key, kv => kv.Value);
                        tempnames = dict["names"];
                        break;
                    default:
                        break;
                }
                string pattern = @"(?<=\')(\D+)(?=\')";
                MatchCollection matches = Regex.Matches(tempnames, pattern);
                labelNames = new string[matches.Count];
                labelNames = new string[matches.Count];
                for (int i = 0; i < matches.Count; i++)
                {
                    labelNames[i] = matches[i].Value;
                }
            }
            catch { }
        }

        /// <summary>
        /// Reads models from IR / ONNX / PDPD / TF / TFLite formats.
        /// </summary>
        /// <param name="model_path">String with a model in IR / ONNX / PDPD / TF / TFLite format.</param>
        /// <param name="weights">Shared pointer to a constant tensor with weights.</param>
        /// <remarks>
        /// Created model object shares the weights with the @p weights object.
        /// Thus, do not create @p weights on temporary data that can be freed later, since the model constant data will point to an invalid memory.
        /// </remarks>
        /// <returns>A model.</returns>
        public Model read_model(string model_path, Tensor weights) 
        {
            IntPtr model_ptr = new IntPtr();
            sbyte[] c_model_path = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(model_path));
            ExceptionStatus status = NativeMethods.ov_core_read_model_from_memory(m_ptr, ref c_model_path[0], weights.Ptr, ref model_ptr);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core read_model() error : " + status.ToString());
                return new Model(IntPtr.Zero);
            }
            return new Model(model_ptr);
        }

        /// <summary>
        /// Creates a compiled model from a source model object.
        /// </summary>
        /// <param name="model">Model object acquired from Core::read_model.</param>
        /// <returns>A compiled model.</returns>
        /// <remarks>
        /// Users can create as many compiled models as they need and use
        /// them simultaneously (up to the limitation of the hardware resources).
        /// </remarks>
        public CompiledModel compile_model(Model model)
        {

            IntPtr compiled_model_ptr = new IntPtr();
            string device_name = "AUTO";
            sbyte[] c_device = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(device_name));
            ExceptionStatus status = NativeMethods.ov_core_compile_model(m_ptr, model.m_ptr, ref c_device[0], 0, ref compiled_model_ptr);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core compiled_model() error : " + status.ToString());
                return new CompiledModel(IntPtr.Zero);
            }
            return new CompiledModel(compiled_model_ptr);
        }

        /// <summary>
        /// Creates and loads a compiled model from a source model to the default OpenVINO device selected by the AUTO
        /// </summary>
        /// <param name="model">Model object acquired from Core::read_model.</param>
        /// <param name="device_name">Name of a device to load a model to.</param>
        /// <returns>A compiled model.</returns>
        /// <remarks>
        /// Users can create as many compiled models as they need and use
        /// them simultaneously (up to the limitation of the hardware resources).
        /// </remarks>
        public CompiledModel compile_model(Model model, string device_name) 
        {
            
            IntPtr compiled_model_ptr = new IntPtr();
            sbyte[] c_device = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(device_name));
            ExceptionStatus status = NativeMethods.ov_core_compile_model(m_ptr, model.m_ptr, ref c_device[0], 0, ref compiled_model_ptr);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core compiled_model() error : " + status.ToString());
                return new CompiledModel(IntPtr.Zero);
            }
            return new CompiledModel(compiled_model_ptr);
        }

        /// <summary>
        /// Reads and loads a compiled model from the IR/ONNX/PDPD file to the default OpenVINO device selected by the AUTO plugin.
        /// </summary>
        /// <param name="model_path">Path to a model.</param>
        /// <remarks>
        /// This can be more efficient than using the Core::read_model + Core::compile_model(model_in_memory_object) flow, 
        /// especially for cases when caching is enabled and a cached model is availab
        /// </remarks>
        /// <returns> A compiled model.</returns>
        public CompiledModel compile_model(string model_path)
        {
            IntPtr compiled_model_ptr = new IntPtr();
            string device_name = "AUTO";
            sbyte[] c_model = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(model_path));
            sbyte[] c_device = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(device_name));
            ExceptionStatus status = NativeMethods.ov_core_compile_model_from_file(m_ptr, ref c_model[0], ref c_device[0], 0, ref compiled_model_ptr);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core compiled_model() error : " + status.ToString());
                return new CompiledModel(IntPtr.Zero);
            }
            return new CompiledModel(compiled_model_ptr);
        }


        /// <summary>
        /// Reads a model and creates a compiled model from the IR/ONNX/PDPD file.
        /// </summary>
        /// <param name="model_path">Path to a model.</param>
        /// <param name="device_name">Name of a device to load a model to.</param>
        /// <remarks>
        /// This can be more efficient than using the Core::read_model + Core::compile_model(model_in_memory_object) flow, 
        /// especially for cases when caching is enabled and a cached model is availab
        /// </remarks>
        /// <returns>A compiled model.</returns>
        public CompiledModel compile_model(string model_path, string device_name) 
        {
            IntPtr compiled_model_ptr = new IntPtr();
            sbyte[] c_model = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(model_path));
            sbyte[] c_device = (sbyte[])((Array)System.Text.Encoding.Default.GetBytes(device_name));
            ExceptionStatus status = NativeMethods.ov_core_compile_model_from_file(m_ptr, ref c_model[0], ref c_device[0], 0, ref compiled_model_ptr);
            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core compiled_model() error : " + status.ToString());
                return new CompiledModel(IntPtr.Zero);
            }
            return new CompiledModel(compiled_model_ptr);
        }


        /// <summary>
        /// Returns devices available for inference.
        /// Core objects go over all registered plugins and ask about available devices.
        /// </summary>
        /// <returns>A vector of devices. The devices are returned as { CPU, GPU.0, GPU.1, GNA }.</returns>
        /// <remarks>
        /// If there is more than one device of a specific type, they are enumerated with the .# suffix.
        /// Such enumerated device can later be used as a device name in all Core methods like Core::compile_model,
        /// Core::query_model, Core::set_property and so on.
        /// </remarks>
        public List<string> get_available_devices() 
        {
            int l = Marshal.SizeOf(typeof(ov_available_devices_t));
            IntPtr devices_ptr = Marshal.AllocHGlobal(l);
            ExceptionStatus status = NativeMethods.ov_core_get_available_devices(m_ptr, devices_ptr);

            if (status != 0)
            {
                System.Diagnostics.Debug.WriteLine("Core get_available_devices() error : " + status.ToString());
                return new List<string>();
            }

            var temp1 = Marshal.PtrToStructure(devices_ptr, typeof(ov_available_devices_t));

            ov_available_devices_t devices_s = (ov_available_devices_t)temp1;
            IntPtr[] devices_ptrs = new IntPtr[devices_s.size];
            Marshal.Copy(devices_s.devices, devices_ptrs, 0, (int)devices_s.size);
            List<string> devices = new List<string>();
            for (int i = 0; i < (int)devices_s.size; ++i)
            {
                devices.Add(Marshal.PtrToStringAnsi(devices_ptrs[i]));
            }
            NativeMethods.ov_available_devices_free(devices_ptr);
            return devices;
        }
    }
}
