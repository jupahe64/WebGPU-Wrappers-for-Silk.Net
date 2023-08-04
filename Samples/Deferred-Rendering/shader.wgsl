struct UbScene {		
    ViewProjection: mat4x4<f32>,
}

struct UbModel {		
    Transform: mat4x4<f32>,
    Color: vec3<f32>,
    NormalMapStrength: f32
}

@group(0) @binding(0)
var<uniform> ubScene: UbScene;

@group(1) @binding(0)
var<uniform> ubModel: UbModel;
@group(1) @binding(1)
var uSampler: sampler;
@group(1) @binding(2)
var uAlbedo: texture_2d<f32>;
@group(1) @binding(3)
var uNormal: texture_2d<f32>;

struct Attributes {
    @location(0) Position: vec3<f32>,
    @location(1) Tex0Coord: vec2<f32>,
    @location(2) Normal: vec3<f32>,
    @location(3) Tangent: vec3<f32>,
}

struct Varyings {
    @builtin(position) Position: vec4<f32>,
    @location(0) Tex0Coord: vec2<f32>,
    @location(1) Normal: vec3<f32>,
    @location(2) Tangent: vec3<f32>,
    @location(3) Bitangent: vec3<f32>,
}

struct ColorTargets {
    @location(0) Albedo: vec4<f32>,
    @location(1) Normal: vec4<f32>,
}

@vertex
fn vs_main(
    a: Attributes,
) ->  Varyings {
    let pos = vec4<f32>(a.Position, 1.);

    let nrmTransform = mat3x3(
        ubModel.Transform[0].xyz,
        ubModel.Transform[1].xyz,
        ubModel.Transform[2].xyz
    );

    let nrm = normalize(nrmTransform * a.Normal);
    let tan = normalize(nrmTransform * a.Tangent);

    return Varyings(
        ubScene.ViewProjection * ubModel.Transform * pos,
        a.Tex0Coord,
        nrm, tan, cross(tan, nrm)
    );
}

@fragment
fn fs_main(v: Varyings) -> ColorTargets {
    let oAlbedo = textureSample(uAlbedo, uSampler, v.Tex0Coord) * vec4(ubModel.Color, 1.0);

    let normalMap = textureSample(uNormal, uSampler, v.Tex0Coord).xyz;
    let nrm = 
        fma(normalMap.r, 2.0, -1.0) * v.Tangent +
        fma(normalMap.g, 2.0, -1.0) * v.Bitangent +
        fma(normalMap.b, 2.0, -1.0) * v.Normal;

    let oNormal = vec4(mix(v.Normal, nrm, ubModel.NormalMapStrength) * 0.5 + vec3<f32>(0.5), 0.0);

    return ColorTargets(oAlbedo, oNormal);
} 