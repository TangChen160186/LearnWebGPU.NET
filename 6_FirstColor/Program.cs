﻿using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;

namespace _6_FirstColor
{
    internal unsafe class Program
    {
        private static IWindow _window;

        private static WebGPU _wgpu;
        private static Instance* _instance;
        private static Adapter* _adapter;
        private static Device* _device;
        private static Queue* _queue;
        private static Surface* _surface;

        private static SurfaceCapabilities _surfaceCapabilities;
        static void Main(string[] args)
        {
            _window = Window.Create(WindowOptions.Default with{
                API = GraphicsAPI.None,
  
            });
            _window.Load += WindowLoad;
            _window.Render += WindowRender;
            _window.Closing += WindowClosing;

            _window.Initialize();
            _window.Center();
            _window.Run();
        }
        private static void WindowLoad()
        {
            _wgpu = WebGPU.GetApi(); 
          
            // create instance
            _instance = _wgpu.CreateInstance(new InstanceDescriptor());
            if (_instance is null)
                throw new Exception("Instance create failure");

            // create surface
            _surface = _window.CreateWebGPUSurface(_wgpu, _instance);
            // request adapter
            var pfnRequestAdapterCallback=PfnRequestAdapterCallback.From((status, adapter, message, _) =>
            {
                if (status == RequestAdapterStatus.Success)
                    _adapter = adapter;
                else
                    throw new Exception(Marshal.PtrToStringUTF8((IntPtr)message));
            });
            _wgpu.InstanceRequestAdapter(_instance, new RequestAdapterOptions(){CompatibleSurface = _surface}, pfnRequestAdapterCallback, default);
            InspectAdapter();


            // request device
            var deviceDescriptor = new DeviceDescriptor
            {
                RequiredFeatureCount = 0,
                RequiredFeatures = null,
                RequiredLimits = null,
                DeviceLostCallback = PfnDeviceLostCallback.From(OnDeviceLostCallBack)
            };
            var pfnRequestDeviceCallback = PfnRequestDeviceCallback.From((status, device, message, _) =>
            {
                if (status == RequestDeviceStatus.Success)
                    _device = device;
                else
                    throw new Exception(Marshal.PtrToStringUTF8((IntPtr)message));
            });
            _wgpu.AdapterRequestDevice(_adapter, deviceDescriptor, pfnRequestDeviceCallback,null);
            var pfnErrorCallback = PfnErrorCallback.From((errorType, message, _) =>
                Console.WriteLine($"Error type:{errorType},{Marshal.PtrToStringUTF8((IntPtr)message)}"));
            _wgpu.DeviceSetUncapturedErrorCallback(_device, pfnErrorCallback, null);
            InspectDevice();

            // get surface capabilities
            _wgpu.SurfaceGetCapabilities(_surface, _adapter, ref _surfaceCapabilities);
            InspectSurfaceCapabilities();


            // get queue
            _queue = _wgpu.DeviceGetQueue(_device);

            // config surface
            ConfigureSurface();
        }
        
        private static void WindowRender(double obj)
        {
            TextureView* targetView = GetNextSurfaceTextureView();
            if (targetView == null) return; // skip this frame

            CommandEncoder* encoder = _wgpu.DeviceCreateCommandEncoder(_device, new CommandEncoderDescriptor());

            RenderPassColorAttachment colorAttachment = new RenderPassColorAttachment()
            {
                LoadOp = LoadOp.Clear,
                StoreOp = StoreOp.Store,
                View = targetView,
                ClearValue = new Color(0.9f, 0.1f, 0.5f, 1.0f)
            };

            var renderPassDescriptor = new RenderPassDescriptor()
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthStencilAttachment = null,
                TimestampWrites = null,
            };

            RenderPassEncoder* renderPass = _wgpu.CommandEncoderBeginRenderPass(encoder, renderPassDescriptor);

            // End and release render pass
            _wgpu.RenderPassEncoderEnd(renderPass);
            _wgpu.RenderPassEncoderRelease(renderPass);

            // Create command buffer
            CommandBuffer* commandBuffer = _wgpu.CommandEncoderFinish(encoder, new CommandBufferDescriptor());
            _wgpu.CommandEncoderRelease(encoder);

            // Submit command buffer
            _wgpu.QueueSubmit(_queue, 1, &commandBuffer);
            _wgpu.CommandBufferRelease(commandBuffer);

            // Present surface
            _wgpu.QueueOnSubmittedWorkDone(_queue,PfnQueueWorkDoneCallback.From((status, _) =>
            {

            }),null);
            // Release texture view
            _wgpu.TextureViewRelease(targetView);
            _wgpu.SurfacePresent(_surface);
            
        }

        private static void WindowClosing()
        {
            _wgpu.QueueRelease(_queue);
            _wgpu.DeviceRelease(_device);
            _wgpu.SurfaceRelease(_surface);
            _wgpu.AdapterRelease(_adapter);
            _wgpu.InstanceRelease(_instance);

            // you can only call Dispose to release all unmanaged resources
            //_wgpu.Dispose();
        }

        private static void InspectAdapter()
        {
            // get adapter limits
            Console.WriteLine("Adapter limits:");
            SupportedLimits supportedLimits = new SupportedLimits();
            if (_wgpu.AdapterGetLimits(_adapter, ref supportedLimits))
            {
                foreach (var fieldInfo in supportedLimits.Limits.GetType().GetFields())
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(supportedLimits.Limits)}");
                }
            }

            // get adapter features
            Console.WriteLine("Adapter features:");
            int featureCount= (int)_wgpu.AdapterEnumerateFeatures(_adapter, null);
            FeatureName* featureNames = stackalloc FeatureName[featureCount];
            _wgpu.AdapterEnumerateFeatures(_adapter, featureNames);
            for (int i = 0; i < featureCount; i++)
            {
                Console.WriteLine($"\t{featureNames[i]}");
            }

            // get adapter properties
            Console.WriteLine("Adapter properties:");
            AdapterProperties properties = new AdapterProperties();
            _wgpu.AdapterGetProperties(_adapter,ref properties);
            foreach (var fieldInfo in properties.GetType().GetFields())
            {
                if (fieldInfo.FieldType.IsPointer)
                {
                    IntPtr value = new IntPtr(Pointer.Unbox(fieldInfo.GetValue(properties)));
                    if(value!= IntPtr.Zero)
                        Console.WriteLine($"\t{fieldInfo.Name}: {Marshal.PtrToStringUTF8(value)}");
                }
                else
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(properties)}");
                }
               
            }
        }

        private static void InspectDevice()
        {
            // get device limits
            Console.WriteLine("Device limits:");
            SupportedLimits supportedLimits = new SupportedLimits();
            if (_wgpu.DeviceGetLimits(_device, ref supportedLimits))
            {
                foreach (var fieldInfo in supportedLimits.Limits.GetType().GetFields())
                {
                    Console.WriteLine($"\t{fieldInfo.Name}: {fieldInfo.GetValue(supportedLimits.Limits)}");
                }
            }

            // get device features
            Console.WriteLine("Device features:");
            int featureCount = (int)_wgpu.DeviceEnumerateFeatures(_device, null);
            FeatureName* featureNames = stackalloc FeatureName[featureCount];
            _wgpu.AdapterEnumerateFeatures(_adapter, featureNames);
            for (int i = 0; i < featureCount; i++)
            {
                Console.WriteLine($"\t{featureNames[i]}");
            }
        }

        private static void InspectSurfaceCapabilities()
        {
            Console.WriteLine("SurfaceCapabilities Support AlphaMode:");
            for (int i = 0; i < (int)_surfaceCapabilities.AlphaModeCount; i++)
            {
                Console.WriteLine($"\t{_surfaceCapabilities.AlphaModes[i]}");
            }
            
            Console.WriteLine("SurfaceCapabilities Support Format:");
            for (int i = 0; i < (int)_surfaceCapabilities.FormatCount; i++)
            {
                Console.WriteLine($"\t{_surfaceCapabilities.Formats[i]}");
            }

            Console.WriteLine("SurfaceCapabilities Support Format:");
            for (int i = 0; i < (int)_surfaceCapabilities.PresentModeCount; i++)
            {
                Console.WriteLine($"\t{_surfaceCapabilities.PresentModes[i]}");
            }
        }
        private static void OnDeviceLostCallBack(DeviceLostReason reason, byte* message, void* _)
        {
            throw new Exception($"lost reason:{reason},{Marshal.PtrToStringUTF8((IntPtr)message)}");
        }

        private static TextureView* GetNextSurfaceTextureView()
        {
            SurfaceTexture surfaceTexture; 
            _wgpu.SurfaceGetCurrentTexture(_surface, &surfaceTexture);
            switch (surfaceTexture.Status)
            {
                case SurfaceGetCurrentTextureStatus.Lost:
                case SurfaceGetCurrentTextureStatus.Outdated:
                case SurfaceGetCurrentTextureStatus.Timeout:
                    _wgpu.TextureRelease(surfaceTexture.Texture);
                    ConfigureSurface();
                    return null;

                case SurfaceGetCurrentTextureStatus.OutOfMemory:
                case SurfaceGetCurrentTextureStatus.DeviceLost:
                case SurfaceGetCurrentTextureStatus.Force32:
                    throw new Exception($"Could not get current surface texture: {surfaceTexture.Status}");
            }


            TextureViewDescriptor textureViewDescriptor = new TextureViewDescriptor()
            {
                NextInChain = null,
                Format = _wgpu.TextureGetFormat(surfaceTexture.Texture),
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All,
            };
            TextureView* targetView = _wgpu.TextureCreateView(surfaceTexture.Texture, textureViewDescriptor);
            return targetView;
        }

        private static void ConfigureSurface()
        {
            TextureFormat textureFormat = _wgpu.SurfaceGetPreferredFormat(_surface,_adapter);
            SurfaceConfiguration surfaceConfiguration = new SurfaceConfiguration()
            {
                AlphaMode = CompositeAlphaMode.Opaque,
                Device = _device,
                Width = (uint)_window.FramebufferSize.X,
                Height = (uint)_window.FramebufferSize.Y,
                PresentMode = PresentMode.Fifo,
                Format = textureFormat,
                ViewFormatCount = 0,
                ViewFormats = null,
                Usage = TextureUsage.RenderAttachment,
                NextInChain = null,
            };
            _wgpu.SurfaceConfigure(_surface, in surfaceConfiguration);
        }
    }
}