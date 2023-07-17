struct UbScene {		
    ViewProjection: mat4x4<f32>,
}

struct UbModel {		
    Transform: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> ubScene: UbScene;

@group(1) @binding(0)
var<uniform> ubModel: UbModel;
@group(1) @binding(1)
var textureSampler: sampler;
@group(1) @binding(2)
var tex0: texture_2d<f32>;

struct Attributes {
    @location(0) Position: vec3<f32>,
    @location(1) Tex0Coord: vec2<f32>,
}

struct Varyings {
    @builtin(position) Position: vec4<f32>,
    @location(0) Tex0Coord: vec2<f32>,
}

@vertex
fn vs_main(
    a: Attributes,
) ->  Varyings {
    let pos = vec4<f32>(a.Position, 1.);
    return Varyings(
        ubScene.ViewProjection * ubModel.Transform * pos,
        a.Tex0Coord
    );
}

@fragment
fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
    return textureSample(tex0, textureSampler, v.Tex0Coord);
} 