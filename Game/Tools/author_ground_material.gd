extends SceneTree

func sample_value(x, y, band):
    var value = (int(x) * 73856093) ^ (int(y) * 19349663) ^ (int(band) * 83492791) ^ 424242
    value = ((value ^ (value >> 16)) * 73244475) & 4294967295
    value = ((value ^ (value >> 16)) * 73244475) & 4294967295
    value = value ^ (value >> 16)
    return float(value & 65535) / 65535.0

func _initialize():
    var image = Image.create(128, 128, false, Image.FORMAT_RGBA8)
    for x in range(128):
        for y in range(128):
            var gx = float(x) / 16.0
            var gy = float(y) / 16.0
            var ix = int(floor(gx))
            var iy = int(floor(gy))
            var tx = smoothstep(0, 1, gx-ix)
            var ty = smoothstep(0, 1, gy-iy)
            var a = lerp(sample_value(ix%8, iy%8, 1), sample_value((ix+1)%8, iy%8, 1), tx)
            var b = lerp(sample_value(ix%8, (iy+1)%8, 1), sample_value((ix+1)%8, (iy+1)%8, 1), tx)
            var value = 0.94 + 0.04 * lerp(a,b,ty) + 0.02 * sample_value(x,y,2)
            image.set_pixel(x,y,Color(value,value,value,1))
    image.generate_mipmaps()
    var texture = ImageTexture.create_from_image(image)
    if ResourceSaver.save(texture,"res://Assets/Scenery/Materials/field-earth-grain.tres") != OK:
        quit(1)
        return
    print("GROUND_MATERIAL_AUTHOR_PASSED")
    quit()
