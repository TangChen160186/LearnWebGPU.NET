
struct UniformData{
    uModelMatrix: mat4x4f,
    uViewMatrix: mat4x4f,
    uProjectionMatrix: mat4x4f,
}
@group(0) @binding(0) var<uniform> uniformData : UniformData;
@group(0) @binding(1) var textureSampler: sampler;
@group(0) @binding(2) var gradientTexture: texture_2d<f32>;
struct VertexInput{
    @location(0)  position: vec3f,
    @location(1)  uv: vec2f,
}

struct VertexOutput{
    @builtin(position)  position: vec4f,
    @location(0)  uv: vec2f,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out:VertexOutput;
    out.uv = in.uv;
    out.position = uniformData.uProjectionMatrix* uniformData.uViewMatrix * uniformData.uModelMatrix * vec4f(in.position,1.0);
	return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    let color = textureSample(gradientTexture, textureSampler, in.uv).rgb;
	return vec4f(color,1.0);
}