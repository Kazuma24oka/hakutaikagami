extends SceneTree
func _initialize():
    call_deferred("show_main")
func show_main():
    var main=load("res://Scenes/Main.tscn").instantiate()
    root.add_child(main)
    var tabs=main.get_child(0)
    assert(tabs.get_tab_title(3)=="3D武将・配送")
    tabs.current_tab=3
    for i in range(12):
        await process_frame
    await RenderingServer.frame_post_draw
    assert(tabs.get_child(3).find_child("character-001",true,false)!=null)
    var path=ProjectSettings.globalize_path("res://../Art/Officer3D/TrialV2/runtime-local/main-game.png")
    assert(root.get_texture().get_image().save_png(path)==OK)
    print("V2_MAIN_TAB_PASS")
    quit()
