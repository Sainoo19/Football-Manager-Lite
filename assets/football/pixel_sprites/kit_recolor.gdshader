shader_type canvas_item;
render_mode unshaded;

uniform sampler2D kit_mask : filter_nearest, repeat_disable;
uniform vec4 shirt_color : source_color = vec4(0.2, 0.4, 0.9, 1.0);
uniform vec4 shorts_color : source_color = vec4(1.0);
uniform vec4 socks_color : source_color = vec4(0.2, 0.4, 0.9, 1.0);
uniform float step_offset = 0.0;

void fragment() {
    vec2 sample_uv = UV;
    // Prototype stepping from the approved idle artwork; sample the same UV in the aligned mask.
    if (UV.y > 0.72) {
        sample_uv.y += step_offset * (UV.x < 0.5 ? 1.0 : -1.0) / 72.0;
    }
    vec4 base = texture(TEXTURE, sample_uv);
    vec3 mask = texture(kit_mask, sample_uv).rgb;
    float region = clamp(mask.r + mask.g + mask.b, 0.0, 1.0);
    vec3 kit = shirt_color.rgb * mask.r + shorts_color.rgb * mask.g + socks_color.rgb * mask.b;
    float shade = clamp(dot(base.rgb, vec3(0.299, 0.587, 0.114)) / 0.62, 0.25, 1.4);
    COLOR = vec4(mix(base.rgb, clamp(kit * shade, 0.0, 1.0), region), base.a);
}
