using Aetherphone.Harness.Rendering;
using Dalamud.Bindings.ImGui;
using Silk.NET.OpenGL;

namespace Aetherphone.Harness.Viewer;

internal sealed unsafe class OpenGlRenderer : IDisposable
{
    private const int VertexStride = 20;
    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 Position;
        layout (location = 1) in vec2 UV;
        layout (location = 2) in vec4 Color;
        uniform mat4 ProjMtx;
        out vec2 Frag_UV;
        out vec4 Frag_Color;
        void main()
        {
            Frag_UV = UV;
            Frag_Color = Color;
            gl_Position = ProjMtx * vec4(Position.xy, 0, 1);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec2 Frag_UV;
        in vec4 Frag_Color;
        uniform sampler2D Texture;
        layout (location = 0) out vec4 Out_Color;
        void main()
        {
            Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
        }
        """;

    private readonly GL gl;
    private readonly TextureStore textures;
    private readonly Dictionary<nint, uint> uploaded = new();
    private readonly List<nint> removedScratch = new();
    private readonly float[] projection = new float[16];
    private readonly uint program;
    private readonly uint vertexArray;
    private readonly uint vertexBuffer;
    private readonly uint indexBuffer;
    private readonly uint whiteTexture;
    private readonly int projectionLocation;
    private readonly int textureLocation;

    public OpenGlRenderer(GL gl, TextureStore textures)
    {
        this.gl = gl;
        this.textures = textures;
        program = BuildProgram();
        projectionLocation = gl.GetUniformLocation(program, "ProjMtx");
        textureLocation = gl.GetUniformLocation(program, "Texture");
        vertexArray = gl.GenVertexArray();
        vertexBuffer = gl.GenBuffer();
        indexBuffer = gl.GenBuffer();
        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        gl.EnableVertexAttribArray(0);
        gl.EnableVertexAttribArray(1);
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, VertexStride, (void*)0);
        gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, VertexStride, (void*)8);
        gl.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, VertexStride, (void*)16);
        gl.BindVertexArray(0);
        whiteTexture = Upload(CpuTexture.Solid(1, 1, 255, 255, 255, 255));
    }

    public void Render(ImDrawDataPtr drawData, int framebufferWidth, int framebufferHeight)
    {
        var displayWidth = drawData.DisplaySize.X;
        var displayHeight = drawData.DisplaySize.Y;
        if (framebufferWidth <= 0 || framebufferHeight <= 0 || displayWidth <= 0f || displayHeight <= 0f)
        {
            return;
        }

        ReleaseRemoved();
        var scaleX = framebufferWidth / displayWidth;
        var scaleY = framebufferHeight / displayHeight;
        gl.Enable(EnableCap.Blend);
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.ScissorTest);
        gl.Viewport(0, 0, (uint)framebufferWidth, (uint)framebufferHeight);
        FillProjection(drawData.DisplayPos, displayWidth, displayHeight);
        gl.UseProgram(program);
        gl.Uniform1(textureLocation, 0);
        fixed (float* matrix = projection)
        {
            gl.UniformMatrix4(projectionLocation, 1, false, matrix);
        }

        gl.BindVertexArray(vertexArray);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
        gl.ActiveTexture(TextureUnit.Texture0);
        var displayPos = drawData.DisplayPos;
        var listCount = drawData.CmdListsCount;
        var lists = drawData.CmdLists;
        for (var listIndex = 0; listIndex < listCount; listIndex++)
        {
            var list = lists[listIndex];
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(list->VtxBuffer.Size * VertexStride), list->VtxBuffer.Data, BufferUsageARB.StreamDraw);
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(list->IdxBuffer.Size * sizeof(ushort)), list->IdxBuffer.Data, BufferUsageARB.StreamDraw);
            var commands = list->CmdBuffer.Data;
            var commandCount = list->CmdBuffer.Size;
            for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                var command = commands[commandIndex];
                if (command.UserCallback != null)
                {
                    continue;
                }

                var clipMinX = (command.ClipRect.X - displayPos.X) * scaleX;
                var clipMinY = (command.ClipRect.Y - displayPos.Y) * scaleY;
                var clipMaxX = (command.ClipRect.Z - displayPos.X) * scaleX;
                var clipMaxY = (command.ClipRect.W - displayPos.Y) * scaleY;
                if (clipMaxX <= clipMinX || clipMaxY <= clipMinY)
                {
                    continue;
                }

                gl.Scissor((int)clipMinX, (int)(framebufferHeight - clipMaxY), (uint)(clipMaxX - clipMinX), (uint)(clipMaxY - clipMinY));
                gl.BindTexture(TextureTarget.Texture2D, Resolve((nint)command.TextureId.Handle));
                gl.DrawElements(PrimitiveType.Triangles, command.ElemCount, DrawElementsType.UnsignedShort, (void*)(command.IdxOffset * sizeof(ushort)));
            }
        }

        gl.Disable(EnableCap.ScissorTest);
        gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        foreach (var texture in uploaded.Values)
        {
            gl.DeleteTexture(texture);
        }

        uploaded.Clear();
        gl.DeleteTexture(whiteTexture);
        gl.DeleteBuffer(vertexBuffer);
        gl.DeleteBuffer(indexBuffer);
        gl.DeleteVertexArray(vertexArray);
        gl.DeleteProgram(program);
    }

    private void FillProjection(Vector2 displayPos, float displayWidth, float displayHeight)
    {
        var left = displayPos.X;
        var right = displayPos.X + displayWidth;
        var top = displayPos.Y;
        var bottom = displayPos.Y + displayHeight;
        Array.Clear(projection);
        projection[0] = 2f / (right - left);
        projection[5] = 2f / (top - bottom);
        projection[10] = -1f;
        projection[12] = (right + left) / (left - right);
        projection[13] = (top + bottom) / (bottom - top);
        projection[15] = 1f;
    }

    private uint Resolve(nint handle)
    {
        if (uploaded.TryGetValue(handle, out var existing))
        {
            return existing;
        }

        if (!textures.TryGet(handle, out var texture))
        {
            return whiteTexture;
        }

        var created = Upload(texture);
        uploaded[handle] = created;
        return created;
    }

    private uint Upload(CpuTexture texture)
    {
        var id = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, id);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        fixed (byte* pixels = texture.Rgba)
        {
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)texture.Width, (uint)texture.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }

        return id;
    }

    private void ReleaseRemoved()
    {
        textures.DrainRemoved(removedScratch);
        for (var index = 0; index < removedScratch.Count; index++)
        {
            if (uploaded.Remove(removedScratch[index], out var id))
            {
                gl.DeleteTexture(id);
            }
        }

        removedScratch.Clear();
    }

    private uint BuildProgram()
    {
        var vertex = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        var fragment = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);
        var created = gl.CreateProgram();
        gl.AttachShader(created, vertex);
        gl.AttachShader(created, fragment);
        gl.LinkProgram(created);
        gl.GetProgram(created, ProgramPropertyARB.LinkStatus, out var linked);
        if (linked == 0)
        {
            throw new InvalidOperationException("Shader link failed: " + gl.GetProgramInfoLog(created));
        }

        gl.DetachShader(created, vertex);
        gl.DetachShader(created, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        return created;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            throw new InvalidOperationException($"{type} compile failed: " + gl.GetShaderInfoLog(shader));
        }

        return shader;
    }
}
