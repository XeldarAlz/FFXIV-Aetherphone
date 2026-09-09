using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using GfxKernel = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using GfxScene = FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using GfxGui = FFXIVClientStructs.FFXIV.Component.GUI;
using GameControl = FFXIVClientStructs.FFXIV.Client.Game.Control;
using NumericsMatrix4x4 = System.Numerics.Matrix4x4;

namespace Aetherphone.Core.Video;

internal sealed unsafe class ScreenPainter : IDisposable
{
	private const float BaseWidth = 1.0f;
	private const float BaseHeight = 0.6f;

	internal Vector3 WorldPosition;
	internal float WorldYaw;
	internal float WorldPitch;
	internal float WorldRoll;
	internal float Scale = 1.0f;
	internal bool Visible { get; set; } = true;
	internal bool Curved { get; set; } = true;

	private readonly VertexShader vertexShader;
	private readonly PixelShader pixelShader;
	private readonly SamplerState samplerState;
	private readonly RasterizerState rasterizerState;
	private readonly DepthStencilState depthTestState;
	private readonly DepthStencilState depthOffState;
	private readonly Buffer constantBuffer;

	private Texture2D? screenTexture;
	private ShaderResourceView? shaderResourceView;

	private nint cachedDepthTexture;
	private ShaderResourceView? cachedDepthView;
	private DepthStencilView? cachedDepthStencilView;
	private bool depthViewUnavailable;
	private bool scaledDepthSkipLogged;

	private const int MaxUiRects = 64;

	private const int CurveSegments = 24;
	private const float CurvedDepth = 0.12f;
	private const int VertexCount = (CurveSegments + 1) * 2;

	[StructLayout(LayoutKind.Sequential)]
	private unsafe struct ScreenParams
	{
		public NumericsMatrix4x4 WorldViewProj;
		public int UiRectCount;
		public float Curvature;
		public float DepthTexelScaleX;
		public float DepthTexelScaleY;
		public fixed float UiRects[MaxUiRects * 4];
	}

	internal ScreenPainter()
	{
		const string hlsl = @"
			#define MAX_UI_RECTS 64
			#define CURVE_SEGMENTS 24
			cbuffer Params : register(b0)
			{
				row_major float4x4 worldViewProj;
				int uiRectCount;
				float curvature;
				float2 depthTexelScale; //backbuffer pixel -> scene depth texel; zero disables the shader depth test
				float4 uiRects[MAX_UI_RECTS]; //xy = screen pos, zw = size, in pixels
			};
			struct VOut { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

			VOut VS(uint id : SV_VertexID)
			{
				uint col = id / 2;
				uint row = id % 2;
				float x = -1.0 + 2.0 * (float)col / (float)CURVE_SEGMENTS;
				float y = row == 0 ? -1.0 : 1.0;
				float z = curvature * x * x; //Parabola: 0 at center, max at the edges.

				VOut o;
				o.pos = mul(float4(x, y, z, 1), worldViewProj);
				o.uv = float2((x + 1.0) * 0.5, row == 0 ? 1.0 : 0.0);
				return o;
			}

			Texture2D tex : register(t0);
			Texture2D<float> sceneDepth : register(t1);
			SamplerState smp : register(s0);

			float4 PS(VOut i, bool isFrontFace : SV_IsFrontFace) : SV_TARGET
			{
				for (int r = 0; r < uiRectCount; r++)
				{
					float4 rect = uiRects[r];
					if (i.pos.x >= rect.x && i.pos.x < rect.x + rect.z &&
						i.pos.y >= rect.y && i.pos.y < rect.y + rect.w)
					{
						discard;
					}
				}

				if (depthTexelScale.x > 0.0)
				{
					int2 texel = int2(i.pos.xy * depthTexelScale);
					float scene = sceneDepth.Load(int3(texel, 0));
					if (i.pos.z < scene) //reverse-Z: the scene is nearer than the screen here
					{
						discard;
					}
				}

				if (!isFrontFace)
				{
					return float4(0.333, 0.333, 0.333, 1); //#555555 - back of the screen, not the (mirrored) video
				}
				return tex.Sample(smp, i.uv);
			}";

		using (var vsb = ShaderBytecode.Compile(hlsl, "VS", "vs_4_0"))
		using (var psb = ShaderBytecode.Compile(hlsl, "PS", "ps_4_0"))
		{
			vertexShader = new VertexShader(DxHandler.Device, vsb);
			pixelShader = new PixelShader(DxHandler.Device, psb);
		}

		samplerState = new SamplerState(DxHandler.Device, new SamplerStateDescription
		{
			Filter = Filter.MinMagMipLinear,
			AddressU = TextureAddressMode.Clamp,
			AddressV = TextureAddressMode.Clamp,
			AddressW = TextureAddressMode.Clamp,
			ComparisonFunction = Comparison.Never,
			MinimumLod = 0,
			MaximumLod = float.MaxValue
		});

		rasterizerState = new RasterizerState(DxHandler.Device, new RasterizerStateDescription
		{
			FillMode = FillMode.Solid,
			CullMode = SharpDX.Direct3D11.CullMode.None
		});

		depthTestState = new DepthStencilState(DxHandler.Device, new DepthStencilStateDescription
		{
			IsDepthEnabled = true,
			DepthWriteMask = DepthWriteMask.Zero,
			DepthComparison = Comparison.GreaterEqual
		});

		depthOffState = new DepthStencilState(DxHandler.Device, new DepthStencilStateDescription
		{
			IsDepthEnabled = false,
			DepthWriteMask = DepthWriteMask.Zero,
			DepthComparison = Comparison.Always
		});

		constantBuffer = new Buffer(DxHandler.Device, sizeof(ScreenParams), ResourceUsage.Default,
			BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

		DxHandler.OnPresent += DrawIfReady;
	}

	internal void SetTarget(Texture2D? texture)
	{
		if (ReferenceEquals(texture, screenTexture))
		{
			return;
		}

		shaderResourceView?.Dispose();
		shaderResourceView = null;
		screenTexture = texture;

		if (texture != null)
		{
			shaderResourceView = new ShaderResourceView(DxHandler.Device, texture, new ShaderResourceViewDescription
			{
				Format = texture.Description.Format,
				Dimension = ShaderResourceViewDimension.Texture2D,
				Texture2D = { MipLevels = texture.Description.MipLevels }
			});
		}
	}

	internal void SetTransform(Vector3 worldPosition, float worldYaw, float worldPitch, float worldRoll, float scale)
	{
		WorldPosition = worldPosition;
		WorldYaw = worldYaw;
		WorldPitch = worldPitch;
		WorldRoll = worldRoll;
		Scale = scale;
	}

	private void DrawIfReady()
	{
		if (!Visible)
		{
			return;
		}

		if (!TryGetSceneTargets(out var targets) || shaderResourceView == null)
		{
			return;
		}

		var worldViewProj = ComputeWorldViewProj();
		if (worldViewProj == null)
		{
			return;
		}

		if (targets.DepthTexture != cachedDepthTexture)
		{
			cachedDepthView?.Dispose();
			cachedDepthView = null;
			cachedDepthStencilView?.Dispose();
			cachedDepthStencilView = null;
			depthViewUnavailable = false;
			cachedDepthTexture = targets.DepthTexture;
		}

		if (cachedDepthView == null && !depthViewUnavailable)
		{
			cachedDepthView = CreateDepthShaderView(targets.DepthTexture);
			depthViewUnavailable = cachedDepthView == null;
		}

		var depthMatchesTarget = targets.RenderWidth == targets.Width && targets.RenderHeight == targets.Height;
		if (cachedDepthView == null)
		{
			if (!depthMatchesTarget)
			{
				if (!scaledDepthSkipLogged)
				{
					scaledDepthSkipLogged = true;
					AepLog.Warning($"[ScreenPainter] The scene depth cannot be sampled and renders at {targets.RenderWidth}x{targets.RenderHeight} against a {targets.Width}x{targets.Height} swap chain; the world screen stays hidden while the render resolution is scaled.");
				}

				return;
			}

			cachedDepthStencilView ??= CreateDepthStencilView(targets.DepthTexture);
			if (cachedDepthStencilView == null)
			{
				return;
			}
		}

		Marshal.AddRef(targets.ColorTexture);
		using var colorResource = new Texture2D(targets.ColorTexture);
		using var rtv = CreateRenderTargetView(colorResource);
		if (rtv == null)
		{
			return;
		}

		var ctx = DxHandler.Device!.ImmediateContext;

		var prevRtvs = ctx.OutputMerger.GetRenderTargets(1, out var prevDsv);
		var prevVs = ctx.VertexShader.Get();
		var prevPs = ctx.PixelShader.Get();
		var prevIl = ctx.InputAssembler.InputLayout;
		var prevTopo = ctx.InputAssembler.PrimitiveTopology;
		var prevBlend = ctx.OutputMerger.BlendState;
		var prevDss = ctx.OutputMerger.DepthStencilState;
		var prevRs = ctx.Rasterizer.State;

		try
		{
			var p = new ScreenParams
			{
				WorldViewProj = worldViewProj.Value,
				Curvature = Curved ? CurvedDepth : 0f,
			};
			if (cachedDepthView != null)
			{
				p.DepthTexelScaleX = (float)targets.RenderWidth / targets.Width;
				p.DepthTexelScaleY = (float)targets.RenderHeight / targets.Height;
			}

			p.UiRectCount = CollectUiRects(ref p);
			var pp = &p;
			ctx.UpdateSubresource(new SharpDX.DataBox((nint)pp), constantBuffer);

			if (cachedDepthView != null)
			{
				ctx.OutputMerger.SetRenderTargets((DepthStencilView?)null, rtv);
				ctx.OutputMerger.DepthStencilState = depthOffState;
			}
			else
			{
				ctx.OutputMerger.SetRenderTargets(cachedDepthStencilView, rtv);
				ctx.OutputMerger.DepthStencilState = depthTestState;
			}

			ctx.Rasterizer.SetViewport(0, 0, targets.Width, targets.Height, 0, 1);
			ctx.InputAssembler.InputLayout = null;
			ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
			ctx.Rasterizer.State = rasterizerState;
			ctx.OutputMerger.BlendState = null;
			ctx.VertexShader.Set(vertexShader);
			ctx.VertexShader.SetConstantBuffer(0, constantBuffer);
			ctx.PixelShader.Set(pixelShader);
			ctx.PixelShader.SetConstantBuffer(0, constantBuffer);
			ctx.PixelShader.SetShaderResource(0, shaderResourceView);
			ctx.PixelShader.SetShaderResource(1, cachedDepthView);
			ctx.PixelShader.SetSampler(0, samplerState);
			ctx.Draw(VertexCount, 0);
			ctx.PixelShader.SetShaderResource(0, null);
			ctx.PixelShader.SetShaderResource(1, null);
		}
		finally
		{
			ctx.OutputMerger.SetRenderTargets(prevDsv, prevRtvs);
			foreach (var prevRtv in prevRtvs)
			{
				prevRtv?.Dispose();
			}
			prevDsv?.Dispose();

			ctx.VertexShader.Set(prevVs); prevVs?.Dispose();
			ctx.PixelShader.Set(prevPs); prevPs?.Dispose();
			ctx.InputAssembler.InputLayout = prevIl; prevIl?.Dispose();
			ctx.InputAssembler.PrimitiveTopology = prevTopo;
			ctx.OutputMerger.BlendState = prevBlend; prevBlend?.Dispose();
			ctx.OutputMerger.DepthStencilState = prevDss; prevDss?.Dispose();
			ctx.Rasterizer.State = prevRs; prevRs?.Dispose();
		}
	}

	private static int CollectUiRects(ref ScreenParams p)
	{
		var stage = GfxGui.AtkStage.Instance();
		if (stage == null || stage->RaptureAtkUnitManager == null)
		{
			return 0;
		}

		var maxWidth = stage->ScreenSize.Width * 0.9f;
		var maxHeight = stage->ScreenSize.Height * 0.9f;

		var count = 0;
		var entries = stage->RaptureAtkUnitManager->AllLoadedUnitsList.Entries;
		for (var i = 0; i < entries.Length && count < MaxUiRects; i++)
		{
			var unit = entries[i].Value;
			if (unit == null || !unit->IsVisible || unit->Alpha == 0 || unit->RootNode == null || unit->RootNode->Color.A == 0 || unit->WindowNode == null)
			{
				continue;
			}

			FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
			unit->RootNode->GetBounds(&bounds);

			float x = bounds.Pos1.X;
			float y = bounds.Pos1.Y;
			float width = bounds.Pos2.X - bounds.Pos1.X;
			float height = bounds.Pos2.Y - bounds.Pos1.Y;
			if (width <= 0 || height <= 0 || width >= maxWidth || height >= maxHeight)
			{
				continue;
			}

			var baseIndex = count * 4;
			p.UiRects[baseIndex + 0] = x;
			p.UiRects[baseIndex + 1] = y;
			p.UiRects[baseIndex + 2] = width;
			p.UiRects[baseIndex + 3] = height;
			count++;
		}

		return count;
	}

	private readonly record struct SceneTargets(
		nint ColorTexture,
		nint DepthTexture,
		uint Width,
		uint Height,
		uint RenderWidth,
		uint RenderHeight);

	private static bool TryGetSceneTargets(out SceneTargets targets)
	{
		targets = default;

		var device = GfxKernel.Device.Instance();
		if (device == null || device->SwapChain == null)
		{
			return false;
		}

		var backBuffer = device->SwapChain->BackBuffer;
		if (backBuffer == null)
		{
			return false;
		}

		var rtm = FFXIVClientStructs.FFXIV.Client.Graphics.Render.RenderTargetManager.Instance();
		var sceneDepth = rtm != null ? rtm->DepthStencil : null;
		if (rtm == null || sceneDepth == null)
		{
			return false;
		}

		var width = device->SwapChain->Width;
		var height = device->SwapChain->Height;
		if (width == 0 || height == 0)
		{
			return false;
		}

		var renderWidth = sceneDepth->ActualWidth != 0 ? sceneDepth->ActualWidth : rtm->Resolution_Width;
		var renderHeight = sceneDepth->ActualHeight != 0 ? sceneDepth->ActualHeight : rtm->Resolution_Height;
		if (renderWidth == 0 || renderHeight == 0)
		{
			return false;
		}

		var colorTexture = (nint)backBuffer->D3D11Texture2D;
		var depthTexture = (nint)sceneDepth->D3D11Texture2D;
		if (colorTexture == 0 || depthTexture == 0)
		{
			return false;
		}

		targets = new SceneTargets(colorTexture, depthTexture, width, height, renderWidth, renderHeight);
		return true;
	}

	private static RenderTargetView? CreateRenderTargetView(Texture2D colorResource)
	{
		if (DxHandler.Device == null)
		{
			return null;
		}

		try
		{
			return new RenderTargetView(DxHandler.Device, colorResource);
		}
		catch (Exception exception)
		{
			AepLog.Warning($"[ScreenPainter] Could not view the scene colour target: {exception.Message}");
			return null;
		}
	}

	private static ShaderResourceView? CreateDepthShaderView(nint texturePtr)
	{
		if (texturePtr == 0 || DxHandler.Device == null)
		{
			return null;
		}

		try
		{
			Marshal.AddRef(texturePtr);
			using var texture = new Texture2D(texturePtr);
			var description = texture.Description;
			if ((description.BindFlags & BindFlags.ShaderResource) == 0 || description.SampleDescription.Count > 1)
			{
				AepLog.Warning($"[ScreenPainter] The scene depth ({description.Format}, {description.SampleDescription.Count}x) cannot be sampled; using the depth stencil path.");
				return null;
			}

			return new ShaderResourceView(DxHandler.Device, texture, new ShaderResourceViewDescription
			{
				Dimension = ShaderResourceViewDimension.Texture2D,
				Format = DepthShaderFormat(description.Format),
				Texture2D = { MipLevels = 1, MostDetailedMip = 0 },
			});
		}
		catch (Exception exception)
		{
			AepLog.Warning($"[ScreenPainter] Could not sample the scene depth target: {exception.Message}");
			return null;
		}
	}

	private static DepthStencilView? CreateDepthStencilView(nint texturePtr)
	{
		if (texturePtr == 0 || DxHandler.Device == null)
		{
			return null;
		}

		try
		{
			Marshal.AddRef(texturePtr);
			using var texture = new Texture2D(texturePtr);
			return new DepthStencilView(DxHandler.Device, texture, new DepthStencilViewDescription
			{
				Dimension = DepthStencilViewDimension.Texture2D,
				Format = DepthViewFormat(texture.Description.Format),
				Texture2D = { MipSlice = 0 },
			});
		}
		catch (Exception exception)
		{
			AepLog.Warning($"[ScreenPainter] Could not view the scene depth target: {exception.Message}");
			return null;
		}
	}

	private static Format DepthShaderFormat(Format format) => format switch
	{
		Format.R32G8X24_Typeless or Format.D32_Float_S8X24_UInt => Format.R32_Float_X8X24_Typeless,
		Format.R32_Typeless or Format.D32_Float => Format.R32_Float,
		Format.R24G8_Typeless or Format.D24_UNorm_S8_UInt => Format.R24_UNorm_X8_Typeless,
		Format.R16_Typeless or Format.D16_UNorm => Format.R16_UNorm,
		_ => format,
	};

	private static Format DepthViewFormat(Format format) => format switch
	{
		Format.R32G8X24_Typeless or Format.D32_Float_S8X24_UInt => Format.D32_Float_S8X24_UInt,
		Format.R32_Typeless or Format.D32_Float => Format.D32_Float,
		Format.R24G8_Typeless or Format.D24_UNorm_S8_UInt => Format.D24_UNorm_S8_UInt,
		Format.R16_Typeless or Format.D16_UNorm => Format.D16_UNorm,
		_ => format,
	};

	private NumericsMatrix4x4? ComputeWorldViewProj()
	{
		var gameCameraManager = GameControl.CameraManager.Instance();
		if (gameCameraManager == null)
		{
			return null;
		}

		var gameCamera = gameCameraManager->GetActiveCamera();
		if (gameCamera == null)
		{
			return null;
		}

		var camera = &gameCamera->CameraBase.SceneCamera;
		var renderCamera = camera->RenderCamera;
		if (renderCamera == null)
		{
			return null;
		}

		var camPos = ToNumerics(camera->Position);
		var camLookAt = ToNumerics(camera->LookAtVector);

		var view = NumericsMatrix4x4.CreateLookAt(camPos, camLookAt, Vector3.UnitY);
		var proj = CreatePerspectiveFieldOfViewReversedZ(renderCamera->FoV, renderCamera->AspectRatio, renderCamera->NearPlane, renderCamera->FarPlane);

		var world =
			NumericsMatrix4x4.CreateScale(BaseWidth * Scale, BaseHeight * Scale, Scale) *
			NumericsMatrix4x4.CreateFromYawPitchRoll(WorldYaw, WorldPitch, WorldRoll) *
			NumericsMatrix4x4.CreateTranslation(WorldPosition);

		return world * view * proj;
	}

	private static NumericsMatrix4x4 CreatePerspectiveFieldOfViewReversedZ(float fov, float aspect, float near, float far)
	{
		var yScale = 1f / MathF.Tan(fov / 2f);
		var xScale = yScale / aspect;

		return new NumericsMatrix4x4(
			xScale, 0, 0, 0,
			0, yScale, 0, 0,
			0, 0, near / (far - near), -1,
			0, 0, near * far / (far - near), 0);
	}

	private static Vector3 ToNumerics(FFXIVClientStructs.FFXIV.Common.Math.Vector3 v)
		=> Unsafe.As<FFXIVClientStructs.FFXIV.Common.Math.Vector3, Vector3>(ref v);

	public void Dispose()
	{
		DxHandler.OnPresent -= DrawIfReady;

		shaderResourceView?.Dispose();
		cachedDepthView?.Dispose();
		cachedDepthStencilView?.Dispose();
		constantBuffer.Dispose();
		depthOffState.Dispose();
		depthTestState.Dispose();
		rasterizerState.Dispose();
		samplerState.Dispose();
		pixelShader.Dispose();
		vertexShader.Dispose();
	}
}
