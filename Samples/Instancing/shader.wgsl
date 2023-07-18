struct UbScene {		
    ViewProjection: mat4x4<f32>,
}

@group(0) @binding(0)
var<uniform> ubScene: UbScene;

@group(1) @binding(1)
var uTextureSampler: sampler;
@group(1) @binding(2)
var uTex0: texture_2d<f32>;

struct Attributes {
    @location(0) Position: vec3<f32>,
    @location(1) Tex0Coord: vec2<f32>,
    @location(2) TransformRow1: vec4<f32>,
    @location(3) TransformRow2: vec4<f32>,
    @location(4) TransformRow3: vec4<f32>,
}

struct Varyings {
    @builtin(position) Position: vec4<f32>,
    @location(0) Tex0Coord: vec2<f32>,
}

fn mat4x3_fromRows(row1: vec4<f32>, row2: vec4<f32>, row3: vec4<f32>) -> mat4x3<f32> {
    return mat4x3(
        vec3(row1.x, row2.x, row3.x),
        vec3(row1.y, row2.y, row3.y),
        vec3(row1.z, row2.z, row3.z),
        vec3(row1.w, row2.w, row3.w)
    );
}

@vertex
fn vs_main(
    a: Attributes,
) ->  Varyings {
    let pos = vec4<f32>(a.Position, 1.);
    let transform = mat4x3_fromRows(
        a.TransformRow1,
        a.TransformRow2,
        a.TransformRow3
    );
    return Varyings(
        ubScene.ViewProjection * vec4(transform * pos, 1.0),
        a.Tex0Coord
    );
}

@fragment
fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
    return textureSample(uTex0, uTextureSampler, v.Tex0Coord);
} 