using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Safe;
using Safe = Silk.NET.WebGPU.Safe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Silk.NET.WebGPU.Safe.BindGroupEntries;
using static Silk.NET.WebGPU.Safe.BindGroupLayoutEntries;
using Silk.NET.WebGPU.Safe.Extensions;

namespace Picking
{
    internal class Depth_ObjID_PixelReader
    {
        private struct PixelReaderInput
        {
            public Vector2D<uint> PixelPosition;
            private uint _padding0;
            private uint _padding1;
        }
        public struct Result
        {
            public float Depth;
            public uint ObjID;
            private uint _padding0;
            private uint _padding1;
        }

        private BufferPtr _inputBuffer;
        private BufferPtr _resultStorageBuffer;
        private BufferPtr _resultStagingBuffer;
        private BindGroupLayoutPtr _bindGroupLayout;
        private BindGroupPtr? _bindGroup;
        private ComputePipelinePtr _computePipeline;

        private static string s_shaderCode = """
            struct UB_Input {
                PixelPos: vec2<u32>
            }

            struct SB_Result {
                Depth: f32,
                ObjID: u32
            }

            @group(0) @binding(0)
            var<uniform> ubInput: UB_Input;

            @group(0) @binding(1)
            var uDepth: texture_depth_2d;

            @group(0) @binding(2)
            var uObjID: texture_2d<u32>;

            @group(0) @binding(3)
            var<storage, read_write> sbResult: SB_Result;

            @compute
            @workgroup_size(1)
            fn main() {
                sbResult.Depth = textureLoad(uDepth, ubInput.PixelPos, 0);
                sbResult.ObjID = textureLoad(uObjID, ubInput.PixelPos, 0).r;
            }
            """;

        public Depth_ObjID_PixelReader(BufferPtr inputBuffer, BufferPtr resultStorageBuffer, BufferPtr resultStagingBuffer,
            BindGroupLayoutPtr bindGroupLayout, ComputePipelinePtr computePipeline)
        {
            _inputBuffer = inputBuffer;
            _resultStorageBuffer = resultStorageBuffer;
            _resultStagingBuffer = resultStagingBuffer;
            _bindGroupLayout = bindGroupLayout;
            _computePipeline = computePipeline;
        }

        public static Depth_ObjID_PixelReader Create(DevicePtr device)
        {
            var inputBuffer = device.CreateBuffer(BufferUsage.CopyDst | BufferUsage.Uniform, (uint)Unsafe.SizeOf<Result>());

            var resultStorageBuffer = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopySrc, (uint)Unsafe.SizeOf<Result>(),
                label: "PixelReader.ResultStorageBuffer");
            var resultStagingBuffer = device.CreateBuffer(BufferUsage.CopyDst | BufferUsage.MapRead, (uint)Unsafe.SizeOf<Result>(),
                label: "PixelReader.ResultStagingBuffer");

            var bindGroupLayout = device.CreateBindGroupLayout(
                stackalloc BindGroupLayoutEntry[]
                {
                    Buffer(0, ShaderStage.Compute, BufferBindingType.Uniform, (uint)Unsafe.SizeOf<PixelReaderInput>()),
                    Texture(1, ShaderStage.Compute, TextureSampleType.Depth, TextureViewDimension.Dimension2D, multisampled: false),
                    Texture(2, ShaderStage.Compute, TextureSampleType.Uint, TextureViewDimension.Dimension2D, multisampled: false),
                    Buffer(3, ShaderStage.Compute, BufferBindingType.Storage, (uint)Unsafe.SizeOf<Result>()),
                }
            );

            var pipelineLayout = device.CreatePipelineLayout(new ReadOnlySpan<BindGroupLayoutPtr>( bindGroupLayout ));

            var shaderModule = device.CreateShaderModuleWGSL(s_shaderCode, 
                new ReadOnlySpan<Safe.ShaderModuleCompilationHint>(
                    new Safe.ShaderModuleCompilationHint
                    {
                        EntryPoint = "main",
                        PipelineLayout = pipelineLayout
                    }
                ));

            var computePipeline = device.CreateComputePipeline(
                layout: pipelineLayout,
                stage: new ProgrammableStage
                {
                    Constants = Array.Empty<(string, double)>(),
                    Module = shaderModule,
                    EntryPoint = "main"
                }
            );

            return new Depth_ObjID_PixelReader(inputBuffer, resultStorageBuffer, resultStagingBuffer, bindGroupLayout, computePipeline);
        }

        public void ReadPixels(DevicePtr device, QueuePtr queue,
            Vector2D<uint> pixelPosition, TextureViewPtr depthTexture, TextureViewPtr ObjIdTexture)
        {
            queue.WriteBuffer(_inputBuffer, 0, new ReadOnlySpan<PixelReaderInput>(
                new PixelReaderInput
                {
                    PixelPosition = pixelPosition,
                }
            ));

            _bindGroup?.Release();
            _bindGroup = device.CreateBindGroup(_bindGroupLayout,
                new BindGroupEntry[]
                {
                    Buffer(0, _inputBuffer, 0, (uint)Unsafe.SizeOf<PixelReaderInput>()),
                    Texture(1, depthTexture),
                    Texture(2, ObjIdTexture),
                    Buffer(3, _resultStorageBuffer, 0, (uint)Unsafe.SizeOf<Result>())
                }
            );

            var cmd = device.CreateCommandEncoder();
            var pass = cmd.BeginComputePass(null);
            pass.SetPipeline(_computePipeline);
            pass.SetBindGroup(0, _bindGroup.Value, null);
            pass.DispatchWorkgroups(1, 1, 1);
            pass.End();

            cmd.CopyBufferToBuffer(_resultStorageBuffer, 0, _resultStagingBuffer, 0,
                (uint)Unsafe.SizeOf<Result>());

            queue.Submit(new ReadOnlySpan<CommandBufferPtr>(cmd.Finish()));
        }

        public Task RequestResultBufferMapping()
        {
            return _resultStagingBuffer.MapAsync(MapMode.Read, 0, (uint)Unsafe.SizeOf<Result>());
        }

        public Result ReadResultBuffer(Task mapBufferTask)
        {
            if (!mapBufferTask.IsCompleted)
                throw new InvalidOperationException($"mapping the buffer hasn't finished yet, " +
                    $"maybe you forgot to call device.{nameof(DevicePtrWgpu.Poll)}()");

            if (mapBufferTask.IsFaulted)
                throw new Exception("buffer wasn't mapped due to exception", mapBufferTask.Exception);

            var resultSpan = _resultStagingBuffer
                .GetMappedRange<Result>(0, (uint)Unsafe.SizeOf<Result>());
            var result = resultSpan[0];

            _resultStagingBuffer.Unmap();
            return result;
        }
    }
}
