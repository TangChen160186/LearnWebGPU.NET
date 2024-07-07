
struct UniformData{
    uModelMatrix: mat4x4f,
    uViewMatrix: mat4x4f,
    uProjectionMatrix: mat4x4f,
}
@group(0) @binding(0) var<uniform> uniformData : UniformData;
struct VertexInput{
    @location(0)  position: vec3f,
    @location(1)  normal: vec3f,
    @location(2)  color: vec3f,

}

struct VertexOutput{
    @builtin(position)  position: vec4f,
    @location(0)  color: vec3f,
    @location(1)  normal: vec3f,
}

@vertex
fn vs_main(in: VertexInput) -> VertexOutput {
    var out:VertexOutput;
    out.color = in.color;
    out.normal = (uniformData.uModelMatrix * vec4f(in.normal,0f)).xyz;
    out.position = uniformData.uProjectionMatrix* uniformData.uViewMatrix * uniformData.uModelMatrix * vec4f(in.position,1.0);

	return out;
}

@fragment
fn fs_main(in: VertexOutput) -> @location(0) vec4f {
    let lightColor1 = vec3f(1.0, 0.9, 0.6);
    let lightColor2 = vec3f(-0.2, 0f, -0f);
    let lightDirection1 = normalize(vec3f(0.5, -0.9, 0.1));
    let lightDirection2 = normalize(vec3f(0.2, 0.4, 0.3));
    let shading1 = max(0.0, dot(lightDirection1, in.normal));
    let shading2 = max(0.0, dot(lightDirection2, in.normal));
    let shading = shading1 * lightColor1 + shading2 * lightColor2;
    let color = in.color * shading;
	return vec4f(color, 1.0);
}