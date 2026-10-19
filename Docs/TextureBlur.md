# Texture blur

`TextureBlur.Apply(source, destination, material, radius, alphaMode)` filters a captured texture with a separable nine-tap Gaussian kernel. Assign a material using the baked `OpenFairy/Texture Blur` shader; the operation does not locate shaders or own persistent materials. Radius is measured in source texture pixels. Source and destination may be the same texture.

Straight-alpha input is converted to premultiplied color before filtering, then converted back. This prevents hidden RGB values in transparent pixels from producing colored fringes. Choose `Premultiplied` for an already premultiplied capture; its output keeps the same representation. Zero radius copies the texture without conversion. The active render target is restored and all temporary textures are returned.

Object capture and backdrop capture have different visibility rules. This utility only filters the supplied pixels. A capture caller must supply resolved color, include padding around an object's bounds, and present the result with the matching alpha blend. It does not turn a per-object filter into a background blur.

The runtime GPU tests cover both alpha representations, energy conservation, transparent-color isolation, and in-place filtering. They do not verify object capture, UI masks, backdrop composition or camera integration.
