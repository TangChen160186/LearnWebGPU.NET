
struct UniformData{
    uColor: vec3f,
    uTime: f32,
}
@group(0) @binding(0) var<uniform> uniformData : UniformData;
struct VertexInput{
    @location(0)  position: vec2f,
}

struct VertexOutput{
    @builtin(position)  position: vec4f,
    @location(0)  color: vec3f,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out:VertexOutput;
    var offest = vec2f(sin(uniformData.uTime) ,cos(uniformData.uTime))*0.3;
    out.position = vec4f(in.position + offest ,0.0,1.0);
    out.color = uniformData.uColor;
	return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4f {
	return vec4f(in.color, 1.0);
}