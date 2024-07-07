using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace _3_BasicShading
{
    [StructLayout(LayoutKind.Explicit)]
    struct UniformData
    {
        [FieldOffset(0)] public Matrix4x4 Model;
        [FieldOffset(64)] public Matrix4x4 View;
        [FieldOffset(128)] public Matrix4x4 Projection;
    }

    internal unsafe class Program
    {
        private static IWindow _window;

        private static WebGPU _wgpu;
        private static Instance* _instance;
        private static Adapter* _adapter;
        private static Device* _device;
        private static Queue* _queue;
        private static Surface* _surface;
        private static RenderPipeline* _renderPipeline;

        private static Buffer* _vertexBuffer;
        private static Buffer* _indexBuffer;
        private static Buffer* _uniformBuffer;

        private static BindGroup* _bindGroup;

        private static Texture* _depthTexture;
        private static TextureView* _depthTextureView;
        private static SurfaceCapabilities _surfaceCapabilities;

        private static readonly float[] _vertexData =
        [
            // Base
            -0.5f, -0.5f, -0.3f,  0.0f, -1.0f, 0.0f,  1.0f, 1.0f, 1.0f,
            0.5f, -0.5f, -0.3f,  0.0f, -1.0f, 0.0f,  1.0f, 1.0f, 1.0f,
            0.5f,  0.5f, -0.3f,  0.0f, -1.0f, 0.0f,  1.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.3f,  0.0f, -1.0f, 0.0f,  1.0f, 1.0f, 1.0f,
    
            // Side 1
            -0.5f, -0.5f, -0.3f,  0.0f, -0.848f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.5f, -0.5f, -0.3f,  0.0f, -0.848f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.0f,  0.0f,  0.5f,  0.0f, -0.848f, 0.53f,  1.0f, 1.0f, 1.0f,

            // Side 2
            0.5f, -0.5f, -0.3f,  0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.5f,  0.5f, -0.3f,  0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.0f,  0.0f,  0.5f,  0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f,

            // Side 3
            0.5f,  0.5f, -0.3f,  0.0f, 0.848f, 0.53f,  1.0f, 1.0f, 1.0f,
            -0.5f,  0.5f, -0.3f,  0.0f, 0.848f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.0f,  0.0f,  0.5f,  0.0f, 0.848f, 0.53f,  1.0f, 1.0f, 1.0f,

            // Side 4
            -0.5f,  0.5f, -0.3f, -0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.3f, -0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f,
            0.0f,  0.0f,  0.5f, -0.848f, 0.0f, 0.53f,  1.0f, 1.0f, 1.0f
        ];

        private static readonly ushort[] _indexData =
        [
            // Base
            0, 1, 2,
            0, 2, 3,

            // Sides
            4, 5, 6,
            7, 8, 9,
            10, 11, 12,
            13, 14, 15
        ];

        private static Matrix4x4 _modelMat = Matrix4x4.Identity;
        static void Main(string[] args)
        {
            _window = Window.Create(WindowOptions.Default with
            {
                API = GraphicsAPI.None,
                VSync = true
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
            var pfnRequestAdapterCallback = PfnRequestAdapterCallback.From((status, adapter, message, _) =>
            {
                if (status == RequestAdapterStatus.Success)
                    _adapter = adapter;
                else
                    throw new Exception(Marshal.PtrToStringUTF8((IntPtr)message));
            });
            _wgpu.InstanceRequestAdapter(_instance, new RequestAdapterOptions() { CompatibleSurface = _surface },
                pfnRequestAdapterCallback, default);
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
            _wgpu.AdapterRequestDevice(_adapter, deviceDescriptor, pfnRequestDeviceCallback, null);
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

            // InitializeBuffers
            InitializeBuffers();

            //InitializePipeline
            InitializePipeline();

            //BindingResources
            BindingResources();
        }

        private static float _renderTime;

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
                ClearValue = new Color(0.2f, 0.3f, 0.3f, 1.0f),
            };
            RenderPassDepthStencilAttachment depthStencilAttachment = new RenderPassDepthStencilAttachment()
            {
                DepthLoadOp = LoadOp.Clear,
                DepthStoreOp = StoreOp.Store,
                DepthClearValue = 1,
                DepthReadOnly = false,
                
                StencilClearValue = 0,
                StencilReadOnly = true,
                StencilLoadOp = LoadOp.Clear,
                StencilStoreOp = StoreOp.Store,

                View = _depthTextureView,
            };

            var renderPassDescriptor = new RenderPassDescriptor()
            {
                ColorAttachmentCount = 1,
                ColorAttachments = &colorAttachment,
                DepthStencilAttachment = &depthStencilAttachment,
                TimestampWrites = null,
            };

            _renderTime += (float)obj;
            UniformData data = new UniformData();
            _modelMat *= Matrix4x4.CreateRotationZ((float)(Math.PI * 1 / 180));
            data.Model = _modelMat;
            data.View = Matrix4x4.CreateLookAt(new Vector3(0,0,2f),Vector3.Zero, Vector3.UnitY);
            data.Projection = Matrix4x4.CreatePerspectiveFieldOfView(70f * (MathF.PI / 180f), (float)_window.FramebufferSize.X / _window.FramebufferSize.Y, 0.1f, 100f);
            _wgpu.QueueWriteBuffer(_queue, _uniformBuffer, 0, data, (UIntPtr)sizeof(UniformData));

            RenderPassEncoder* renderPass = _wgpu.CommandEncoderBeginRenderPass(encoder, renderPassDescriptor);
            _wgpu.RenderPassEncoderSetBindGroup(renderPass, 0, _bindGroup, 0, 0);
            _wgpu.RenderPassEncoderSetPipeline(renderPass, _renderPipeline);
            _wgpu.RenderPassEncoderSetVertexBuffer(renderPass, 0, _vertexBuffer, 0, _wgpu.BufferGetSize(_vertexBuffer));
            _wgpu.RenderPassEncoderSetIndexBuffer(renderPass, _indexBuffer, IndexFormat.Uint16, 0,
                _wgpu.BufferGetSize(_indexBuffer));
            _wgpu.RenderPassEncoderDrawIndexed(renderPass, (uint)_indexData.Length, 1, 0, 0, 0);

            // End and release render pass
            _wgpu.RenderPassEncoderEnd(renderPass);

            // Create command buffer
            CommandBuffer* commandBuffer = _wgpu.CommandEncoderFinish(encoder, new CommandBufferDescriptor());

            // Submit command buffer
            _wgpu.QueueSubmit(_queue, 1, &commandBuffer);
            _wgpu.CommandBufferRelease(commandBuffer);

            // Present surface
            _wgpu.SurfacePresent(_surface);

            // release unmanaged resource
            _wgpu.TextureViewRelease(targetView);
            _wgpu.RenderPassEncoderRelease(renderPass);
            _wgpu.CommandEncoderRelease(encoder);
        }

        private static void WindowClosing()
        {
            _wgpu.TextureDestroy(_depthTexture);
            _wgpu.TextureRelease(_depthTexture);
            _wgpu.TextureViewRelease(_depthTextureView);
            _wgpu.BindGroupRelease(_bindGroup);
            _wgpu.BufferRelease(_uniformBuffer);
            _wgpu.BufferRelease(_vertexBuffer);
            _wgpu.RenderPipelineRelease(_renderPipeline);
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
            int featureCount = (int)_wgpu.AdapterEnumerateFeatures(_adapter, null);
            FeatureName* featureNames = stackalloc FeatureName[featureCount];
            _wgpu.AdapterEnumerateFeatures(_adapter, featureNames);
            for (int i = 0; i < featureCount; i++)
            {
                Console.WriteLine($"\t{featureNames[i]}");
            }

            // get adapter properties
            Console.WriteLine("Adapter properties:");
            AdapterProperties properties = new AdapterProperties();
            _wgpu.AdapterGetProperties(_adapter, ref properties);
            foreach (var fieldInfo in properties.GetType().GetFields())
            {
                if (fieldInfo.FieldType.IsPointer)
                {
                    IntPtr value = new IntPtr(Pointer.Unbox(fieldInfo.GetValue(properties)));
                    if (value != IntPtr.Zero)
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

            Console.WriteLine("SurfaceCapabilities Support PresentMode:");
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

                    _wgpu.TextureDestroy(_depthTexture);
                    _wgpu.TextureRelease(_depthTexture);
                    _wgpu.TextureViewRelease(_depthTextureView);
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
            TextureFormat textureFormat = _wgpu.SurfaceGetPreferredFormat(_surface, _adapter);
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

            CreateDepthTexture();
        }

        private static void InitializePipeline()
        {
            var codeString = File.ReadAllText("Geometry.wgsl");
            ShaderModuleWGSLDescriptor shaderModuleWgslDescriptor = new ShaderModuleWGSLDescriptor()
            {
                Chain = new ChainedStruct()
                {
                    Next = null,
                    SType = SType.ShaderModuleWgslDescriptor,
                },
                Code = (byte*)SilkMarshal.StringToPtr(codeString)
            };
            ShaderModuleDescriptor shaderModuleDescriptor = new ShaderModuleDescriptor()
            {
                NextInChain = (ChainedStruct*)(&shaderModuleWgslDescriptor)
            };
            ShaderModule* shaderModule = _wgpu.DeviceCreateShaderModule(_device, shaderModuleDescriptor);

            VertexAttribute positionAttribute = new VertexAttribute()
            {
                Format = VertexFormat.Float32x3,
                Offset = 0,
                ShaderLocation = 0
            };
            VertexAttribute normalAttribute = new VertexAttribute()
            {
                Format = VertexFormat.Float32x3,
                Offset = sizeof(float) * 3,
                ShaderLocation = 1
            };
            VertexAttribute colorAttribute = new VertexAttribute()
            {
                Format = VertexFormat.Float32x3,
                Offset = sizeof(float) * 6,
                ShaderLocation = 2
            };

            VertexAttribute* attributes = stackalloc VertexAttribute[] { positionAttribute,normalAttribute,colorAttribute };
            VertexBufferLayout vertexBufferLayout = new VertexBufferLayout()
            {
                AttributeCount = 3,
                Attributes = attributes,
                ArrayStride = sizeof(float) * 9,
                StepMode = VertexStepMode.Vertex,
            };
            VertexState vertexState = new VertexState()
            {
                BufferCount = 1,
                Buffers = &vertexBufferLayout,
                ConstantCount = 0,
                Constants = null,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("vs_main"),
                Module = shaderModule,
                NextInChain = null,
            };


            PrimitiveState primitiveState = new PrimitiveState()
            {
                CullMode = CullMode.None,
                FrontFace = FrontFace.Ccw,
                StripIndexFormat = IndexFormat.Undefined,
                Topology = PrimitiveTopology.TriangleList,
                NextInChain = null,
            };
            BlendState blendState = new BlendState()
            {
                Alpha = new BlendComponent(BlendOperation.Add, BlendFactor.Zero, BlendFactor.One),
                Color = new BlendComponent(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
            };
            ColorTargetState colorTargetState = new ColorTargetState()
            {
                Blend = &blendState,
                Format = _wgpu.SurfaceGetPreferredFormat(_surface, _adapter),
                WriteMask = ColorWriteMask.All,
                NextInChain = null,
            };
            FragmentState fragmentState = new FragmentState()
            {
                ConstantCount = 0,
                Constants = null,
                EntryPoint = (byte*)SilkMarshal.StringToPtr("fs_main"),
                Module = shaderModule,
                TargetCount = 1,
                Targets = &colorTargetState,
                NextInChain = null,
            };
            MultisampleState multisampleState = new MultisampleState()
            {
                Count = 1,
                Mask = ~0u,
                AlphaToCoverageEnabled = false,
                NextInChain = null,
            };
            DepthStencilState depthStencilState = default;
            depthStencilState.Format = TextureFormat.Depth24Plus;
            depthStencilState.DepthWriteEnabled = true;
            depthStencilState.DepthCompare = CompareFunction.Less;
            depthStencilState.StencilBack = new StencilFaceState(CompareFunction.Equal,StencilOperation.DecrementClamp,StencilOperation.DecrementClamp,StencilOperation.DecrementClamp);
            depthStencilState.StencilFront = depthStencilState.StencilBack;

            depthStencilState.StencilReadMask = 0;
            depthStencilState.StencilWriteMask = 0;
            depthStencilState.DepthBias = 0;

            BufferBindingLayout bufferBindingLayout = new BufferBindingLayout()
            {
                Type = BufferBindingType.Uniform,
                HasDynamicOffset = false,
                MinBindingSize = (ulong)sizeof(UniformData)
            };
            BindGroupLayoutEntry bindGroupLayoutEntry = new BindGroupLayoutEntry
            {
                Binding = 0,
                Buffer = bufferBindingLayout,
                Visibility = ShaderStage.Vertex
            };
            BindGroupLayoutDescriptor bindGroupLayoutDescriptor = new BindGroupLayoutDescriptor()
            {
                EntryCount = 1,
                Entries = &bindGroupLayoutEntry,
            };
            BindGroupLayout* bindGroupLayout = _wgpu.DeviceCreateBindGroupLayout(_device, bindGroupLayoutDescriptor);

            PipelineLayoutDescriptor pipelineLayoutDescriptor = new PipelineLayoutDescriptor()
            {
                BindGroupLayoutCount = 1,
                BindGroupLayouts = &bindGroupLayout,
            };
            PipelineLayout* pipelineLayout = _wgpu.DeviceCreatePipelineLayout(_device, pipelineLayoutDescriptor);

            RenderPipelineDescriptor renderPipelineDescriptor = new RenderPipelineDescriptor()
            {
                Vertex = vertexState,
                Primitive = primitiveState,
                Fragment = &fragmentState,
                DepthStencil = &depthStencilState,
                Layout = pipelineLayout,
                Multisample = multisampleState,
                NextInChain = null,
                Label = null,
            };
            _renderPipeline = _wgpu.DeviceCreateRenderPipeline(_device, renderPipelineDescriptor);

            // release unmanaged resource
            _wgpu.PipelineLayoutRelease(pipelineLayout);
            _wgpu.BindGroupLayoutRelease(bindGroupLayout);
            _wgpu.ShaderModuleRelease(shaderModule);
            SilkMarshal.FreeString((IntPtr)vertexState.EntryPoint);
            SilkMarshal.FreeString((IntPtr)fragmentState.EntryPoint);
            SilkMarshal.FreeString((IntPtr)shaderModuleWgslDescriptor.Code);
        }

        private static void InitializeBuffers()
        {
            BufferDescriptor vertexBufferDescriptor = new BufferDescriptor()
            {
                MappedAtCreation = false,
                Size = (ulong)(sizeof(float) * _vertexData.Length),
                Usage = BufferUsage.CopyDst | BufferUsage.Vertex
            };

            _vertexBuffer = _wgpu.DeviceCreateBuffer(_device, vertexBufferDescriptor);
            fixed (void* ptr = _vertexData)
                _wgpu.QueueWriteBuffer(_queue, _vertexBuffer, 0, ptr, (UIntPtr)(sizeof(float) * _vertexData.Length));

            BufferDescriptor indexBufferDescriptor = new BufferDescriptor()
            {
                MappedAtCreation = false,
                Size = (ulong)(sizeof(ushort) * _indexData.Length),
                Usage = BufferUsage.CopyDst | BufferUsage.Index
            };

            _indexBuffer = _wgpu.DeviceCreateBuffer(_device, indexBufferDescriptor);
            fixed (void* ptr = _indexData)
                _wgpu.QueueWriteBuffer(_queue, _indexBuffer, 0, ptr, (UIntPtr)(sizeof(ushort) * _indexData.Length));

            BufferDescriptor uniformBufferDescriptor = new BufferDescriptor()
            {
                MappedAtCreation = false,
                Size = (ulong)(sizeof(UniformData)),
                Usage = BufferUsage.CopyDst | BufferUsage.Uniform
            };

            _uniformBuffer = _wgpu.DeviceCreateBuffer(_device, uniformBufferDescriptor);
        }

        private static void BindingResources()
        {
            BindGroupEntry bindGroupEntry = new BindGroupEntry()
            {
                Binding = 0,
                Buffer = _uniformBuffer,
                Offset = 0,
                Size = (ulong)sizeof(UniformData),
            };
            BindGroupDescriptor bindGroupDescriptor = new BindGroupDescriptor()
            {
                EntryCount = 1,
                Entries = &bindGroupEntry,
                Layout = _wgpu.RenderPipelineGetBindGroupLayout(_renderPipeline, 0),
            };
            _bindGroup = _wgpu.DeviceCreateBindGroup(_device, bindGroupDescriptor);
        }

        private static void CreateDepthTexture()
        {
            TextureFormat depthTextureFormat = TextureFormat.Depth24Plus;

            TextureDescriptor depthTextureDescriptor = new TextureDescriptor()
            {
                Dimension = TextureDimension.Dimension2D,
                Format = depthTextureFormat,
                MipLevelCount = 1,
                SampleCount = 1,
                Size = new Extent3D((uint?)_window.FramebufferSize.X, (uint?)_window.FramebufferSize.Y,1),
                Usage = TextureUsage.RenderAttachment,
                ViewFormatCount = 1,
                ViewFormats = &depthTextureFormat,
            };

            _depthTexture = _wgpu.DeviceCreateTexture(_device, depthTextureDescriptor);


            TextureViewDescriptor depthTextureViewDescriptor = new TextureViewDescriptor()
            {
                ArrayLayerCount = 1,
                BaseArrayLayer =0,
                MipLevelCount = 1,
                BaseMipLevel = 0,
                Aspect = TextureAspect.DepthOnly,
                Dimension = TextureViewDimension.Dimension2D,
                Format = depthTextureFormat,
            };
            _depthTextureView = _wgpu.TextureCreateView(_depthTexture, depthTextureViewDescriptor);
        }
    }
}