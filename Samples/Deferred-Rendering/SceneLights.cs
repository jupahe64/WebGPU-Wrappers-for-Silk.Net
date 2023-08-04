using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeferredRendering
{
    internal class SceneLights
    {
        public uint LightCount => (uint)_lightData.Length;
        public ReadOnlySpan<DeferredShader.LightData> LightData => _lightData;

        private float _time;

        private DeferredShader.LightData[] _lightData = 
            new DeferredShader.LightData[8];

        public SceneLights() => Update();

        private void Update()
        {
            float a = MathF.Sin(_time) * 0.5f + 0.5f;
            float b = MathF.Cos(_time) * 0.5f + 0.5f;
            float c = -MathF.Sin(_time) * 0.5f + 0.5f;
            float d = -MathF.Cos(_time) * 0.5f + 0.5f;

            static DeferredShader.LightData Light(float x, float y, float z, (float r, float g, float b) col, float intensity, float radius)
                => new()
                {
                    Position = new Vector3D<float>(x,y,z),
                    Color = new Vector3D<float>(col.r, col.g, col.b) * intensity,
                    Radius = radius
                };

            int i = 0;

            float sin, cos;

            const float CircleRadius = 4;
            (sin, cos) = MathF.SinCos(_time + MathF.Tau * 0 / 4f);
            _lightData[i++] = Light(
                x: CircleRadius * sin, z: CircleRadius * cos,
                y: 0.5f + a * 4, 

                col: (1, 0.5f, 0),
                intensity: 3, radius: 10
            );
            (sin, cos) = MathF.SinCos(_time + MathF.Tau * 1 / 4f);
            _lightData[i++] = Light(
                x: CircleRadius * sin, z: CircleRadius * cos,
                y: 0.5f + b * 4,

                col: (0, 0.5f, 1),
                intensity: 3, radius: 10
            );
            (sin, cos) = MathF.SinCos(_time + MathF.Tau * 2 / 4f);
            _lightData[i++] = Light(
                x: CircleRadius * sin, z: CircleRadius * cos,
                y: 0.5f + c * 4,

                col: (0.5f, 0, 1),
                intensity: 3, radius: 10
            );
            (sin, cos) = MathF.SinCos(_time + MathF.Tau * 3 / 4f);
            _lightData[i++] = Light(
                x: CircleRadius * sin, z: CircleRadius * cos,
                y: 0.5f + d * 4,

                col: (0, 1, 0.5f),
                intensity: 3, radius: 10
            );

            _lightData[i++] = Light(
                x: -7.5f, y: 0, z: -6,

                col: (1, 1, 1),
                intensity: 2, radius: 10
            );
            _lightData[i++] = Light(
                x: 7.5f, y: 0, z: -6,

                col: (1, 1, 1),
                intensity: 2, radius: 10
            );
            _lightData[i++] = Light(
                x: -7.5f, y: 0, z: 6,

                col: (1, 1, 1),
                intensity: 2, radius: 10
            );
            _lightData[i++] = Light(
                x: 7.5f, y: 0, z: 6,

                col: (1, 1, 1),
                intensity: 2, radius: 10
            );
        }

        public void Animate(float deltaTime)
        {
            _time += deltaTime;
            Update();
        }
    }
}
