using Godot;
// Readback resources belong to the capture operation, not the scene or the finalizer queue.
public static class ViewportCapture
{
    public static Error Save(Viewport viewport,string path)
    {
        using var texture=viewport.GetTexture();
        using var image=texture.GetImage();
        return image.SavePng(path);
    }
}
