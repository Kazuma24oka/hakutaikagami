shader_type spatial;
render_mode cull_disabled;
uniform sampler2D base_texture : source_color;
uniform sampler2D normal_texture : hint_normal;
uniform sampler2D orm_texture;
uniform sampler2D protected_skin_texture : filter_nearest, repeat_disable;
uniform vec3 tint = vec3(1.0);
uniform int kind = 0;
uniform bool face_surface = false;
uniform int debug_mask = 0;
uniform bool selected_gear = false;
uniform bool verification_unlit = false;
vec3 to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), greaterThan(c,vec3(0.04045)));
}
vec3 to_srgb(vec3 c) {
    c=max(c,vec3(0.0));
    return mix(c * 12.92, 1.055 * pow(c,vec3(1.0/2.4)) - 0.055, greaterThan(c,vec3(0.0031308)));
}
void fragment() {
    vec4 base = texture(base_texture, UV);
    if (kind == 0 && base.a < 0.5) discard;
    vec4 paint = face_surface ? texture(protected_skin_texture,UV) : vec4(0.0);
    vec3 source_linear=OUTPUT_IS_SRGB ? to_linear(base.rgb) : base.rgb;
    vec3 color_linear=kind == 1 ? (paint.a >= 1.0 ? source_linear : (source_linear-paint.rgb)*tint+paint.rgb) : source_linear*tint;
    ALBEDO=OUTPUT_IS_SRGB ? to_srgb(color_linear) : color_linear;
    ROUGHNESS = 0.85;
    METALLIC = 0.0;
    SPECULAR = kind == 2 ? 0.5 : 0.0;
    if (kind == 2) {
        vec3 orm = texture(orm_texture,UV).rgb;
        ROUGHNESS = orm.g; METALLIC = orm.b;
        NORMAL_MAP = texture(normal_texture,UV).rgb;
    }
    if (debug_mask > 0) {
        ALBEDO = vec3(0.0); METALLIC = 0.0; ROUGHNESS = 1.0;
        EMISSION = vec3(0.0);
        if (debug_mask == 1 && kind == 1 && paint.a < 1.0) EMISSION = face_surface ? vec3(1,0,0) : vec3(0,1,0);
        if (debug_mask == 2 && kind == 2 && selected_gear) EMISSION = vec3(0,0,1);
    }
    if (verification_unlit && debug_mask == 0) { EMISSION=ALBEDO; ALBEDO=vec3(0.0); }
}
