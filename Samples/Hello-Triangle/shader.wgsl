struct UbScene {		
    ViewProjection: mat4x4<f32>,
}

//@group(0) @binding(0)
//var<uniform> ubScene: UbScene;

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
        pos, //ubScene.ViewProjection * pos,
        a.Tex0Coord
    );
}

@fragment
fn fs_main(v: Varyings) -> @location(0) vec4<f32> {
    return vec4<f32>(v.Tex0Coord, 0., 1.);
} 