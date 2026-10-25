extends SceneTree

func _initialize():
    for variant in range(2):
        var st = SurfaceTool.new()
        st.begin(Mesh.PRIMITIVE_TRIANGLES)
        var bottom = []
        var middle = []
        for i in range(9):
            var angle = TAU * i / 9.0 + variant * 0.17
            var radius = 0.40 + 0.055 * sin(i * 2.31 + variant * 1.7)
            bottom.append(Vector3(cos(angle) * radius, 0, sin(angle) * radius * 0.9))
            middle.append(Vector3(cos(angle + 0.06) * radius * 0.97, 0.33 + 0.09 * cos(i * 1.8 + variant), sin(angle + 0.06) * radius * 0.88))
        var top = Vector3(0.09 if variant == 0 else -0.06, 0.91 if variant == 0 else 0.84, -0.05)
        for i in range(9):
            var j = (i + 1) % 9
            triangle(st, bottom[i], bottom[j], middle[j])
            triangle(st, bottom[i], middle[j], middle[i])
            triangle(st, middle[i], middle[j], top)
            triangle(st, Vector3.ZERO, bottom[j], bottom[i])
        var mesh = st.commit()
        var filename = "res://Assets/Scenery/Models/field-rock-mesh-" + ("a" if variant == 0 else "b") + ".tres"
        if ResourceSaver.save(mesh, filename) != OK:
            quit(1)
            return
    print("FIELD_ROCK_AUTHOR_PASSED")
    quit()

func triangle(st, a, b, c):
    var normal = (b-a).cross(c-a).normalized()
    if normal.dot((a+b+c)/3.0-Vector3(0,0.4,0)) < 0:
        var swap = b
        b = c
        c = swap
        normal = -normal
    st.set_normal(normal)
    st.add_vertex(a)
    st.add_vertex(c)
    st.add_vertex(b)
