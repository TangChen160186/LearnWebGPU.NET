
struct UniformData{
    uColor: vec3f,
    uTime: f32,
    uModelMatrix: mat4x4f,
    uViewMatrix: mat4x4f,
    uProjectionMatrix: mat4x4f,
}
@group(0) @binding(0) var<uniform> uniformData : UniformData;
struct VertexInput{
    @location(0)  position: vec3f,
}

struct VertexOutput{
    @builtin(position)  position: vec4f,
    @location(0)  color: vec3f,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out:VertexOutput;
    out.color = uniformData.uColor;
    var offest = vec3f(0f,0f,clamp(sin(uniformData.uTime),0f,-1f));
    out.position = uniformData.uProjectionMatrix* uniformData.uViewMatrix * uniformData.uModelMatrix * vec4f(in.position + offest,1.0);

	return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4f {
	return vec4f(in.color, 1.0);
}