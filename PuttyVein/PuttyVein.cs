using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;

namespace PuttyVein
{
    [BepInPlugin("me.liantian.plugin.PuttyVein", "PuttyVein", "1.0.1")]
    [BepInProcess("DSPGAME.exe")]
    [BepInDependency("dimava.plugin.Dyson.SquarePatchMod", BepInDependency.DependencyFlags.SoftDependency)]
    public class PuttyVein : BaseUnityPlugin
    {
		private static ConfigEntry<float> VeinLatitude;
		public new static ManualLogSource Logger;
		public static RectTransform pppButton = null;

		public void Awake()
		{
			PuttyVein.VeinLatitude = base.Config.Bind<float>("", "VeinLatitude", 37.5f, "矿物目标纬度");
			PuttyVein.Logger = base.Logger;
			Harmony.CreateAndPatchAll(typeof(PuttyVein));
			// PuttyVein.enabledSprite = PuttyVein.GetSprite(new Color(0f, 1f, 0f));
			// PuttyVein.disabledSprite = PuttyVein.GetSprite(new Color(0.5f, 0.5f, 0.5f));
			PuttyVein.Logger.LogInfo("PuttyVein 初始化");
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameMain), "Begin")]
		public static void GameMain_Begin_Prefix()
		{
			if (GameMain.instance != null && GameObject.Find("Game Menu/button-1-bg") && PuttyVein.pppButton == null)
			{
				RectTransform component = GameObject.Find("Game Menu").GetComponent<RectTransform>();
				RectTransform component2 = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>();
				Vector3 localPosition = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>().localPosition;
				PuttyVein.pppButton = UnityEngine.Object.Instantiate<RectTransform>(component2);
				PuttyVein.pppButton.gameObject.name = "ppp---button";
				UIButton component3 = PuttyVein.pppButton.GetComponent<UIButton>();
				component3.tips.delay = 0f;
				PuttyVein.pppButton.SetParent(component);
				PuttyVein.pppButton.localScale = new Vector3(0.35f, 0.35f, 0.35f);
				PuttyVein.pppButton.localPosition = new Vector3(localPosition.x + 20.5f, localPosition.y + 161f, localPosition.z);
				component3.OnPointerDown(null);
				component3.OnPointerEnter(null);
				component3.button.onClick.AddListener(delegate ()
				{

					PuttyVein.Logger.LogInfo("PuttyVein 点击按钮");

					PlanetData localPlanet = GameMain.localPlanet;

					float f_latitude = PuttyVein.VeinLatitude.Value;
					decimal dec_latitude = new decimal(f_latitude);
					double d_latitude = (double)dec_latitude;

					PuttyVein.Logger.LogInfo(String.Format("纬度 is {0}", d_latitude));


					double radian = Math.PI * d_latitude / 180.0;
					PuttyVein.Logger.LogInfo(String.Format("弧度 is {0}", radian));


					float f_radius = localPlanet.radius;
					decimal dec_radius = new decimal(f_radius);
					double d_radius = (double)dec_radius;
					PuttyVein.Logger.LogInfo(String.Format("星球半径 is {0}", d_radius));

					double siniRadian = Math.Sin(radian);
					double cosiRadian = Math.Cos(radian);
					PuttyVein.Logger.LogInfo(String.Format("正弦 is {0}", siniRadian));
					PuttyVein.Logger.LogInfo(String.Format("余弦 is {0}", cosiRadian));

					double d_veinY = siniRadian * d_radius;
					float f_veinY = (float)d_veinY;
					PuttyVein.Logger.LogInfo(String.Format("矿物的Y值 is {0}", d_veinY));

					double d_veinRadius = cosiRadian * d_radius;
					PuttyVein.Logger.LogInfo(String.Format("矿物的小圆半径是 is {0}", d_veinRadius));



					VeinData[] veinPool = localPlanet.factory.veinPool;
					PlanetData.VeinGroup[] vgPool = localPlanet.veinGroups;


					for (int i = 0; i < Math.Min(veinPool.Length, 800); i++)
					{
						if ((veinPool[i].type != EVeinType.None) && (veinPool[i].type != EVeinType.Oil))
						{
							double vein_angle = Convert.ToDouble(i) / 200 * 90;
							double d_veinX = Math.Sin(Math.PI * vein_angle / 180.0) * d_veinRadius;
							double d_veinZ = Math.Cos(Math.PI * vein_angle / 180.0) * d_veinRadius;
							float f_veinX = (float)d_veinX;
							float f_veinZ = (float)d_veinZ;
							Vector3 vein_pos = new Vector3(f_veinX, f_veinY, f_veinZ);
							veinPool[i].pos = vein_pos;


							int colliderId = veinPool[i].colliderId;
							localPlanet.physics.RemoveColliderData(colliderId);
							veinPool[i].colliderId = localPlanet.physics.AddColliderData(LDB.veins.Select((int)veinPool[i].type).prefabDesc.colliders[0].BindToObject(i, 0, EObjectType.Vein, veinPool[i].pos, Quaternion.FromToRotation(Vector3.up, veinPool[i].pos.normalized)));
							localPlanet.factoryModel.gpuiManager.AlterModel((int)veinPool[i].modelIndex, veinPool[i].modelId, i, veinPool[i].pos, Maths.SphericalRotation(veinPool[i].pos, 90f), true);




							vgPool[veinPool[i].groupIndex].pos = vein_pos;
							PuttyVein.Logger.LogInfo(String.Format("{2} 矿物类型：{0}   矿物坐标 {1} ", veinPool[i].type, vein_pos, i));

						}

					}

					if (veinPool.Length > 800)
					{
						for (int i = 800; i < Math.Min(veinPool.Length, 1600); i++)
						{
							if ((veinPool[i].type != EVeinType.None) && (veinPool[i].type != EVeinType.Oil))
							{

								double vein_angle = Convert.ToDouble(i - 800) / 200 * 90;
								double d_veinX = Math.Sin(Math.PI * vein_angle / 180.0) * d_veinRadius;
								double d_veinZ = Math.Cos(Math.PI * vein_angle / 180.0) * d_veinRadius;
								float f_veinX = (float)d_veinX;
								float f_veinZ = (float)d_veinZ;
								Vector3 vein_pos = new Vector3(f_veinX, -f_veinY, f_veinZ);
								veinPool[i].pos = vein_pos;
								vgPool[veinPool[i].groupIndex].pos = vein_pos;
								PuttyVein.Logger.LogInfo(String.Format("{2} 矿物类型：{0}   矿物坐标 {1} ", veinPool[i].type, vein_pos, i));

							}

						}


					}


					Vector3 player_pos = GameMain.mainPlayer.position;
					PuttyVein.Logger.LogInfo(String.Format("Pos is {0}", player_pos));
					PuttyVein.Logger.LogInfo(localPlanet.radius);


				});
			}
		}

		public static Sprite GetSprite(Color color)
		{
			Texture2D texture2D = new Texture2D(48, 48, TextureFormat.RGBA32, false);
			ulong[] array = new ulong[]
			{
				496UL,
				1008UL,
				4080UL,
				16368UL,
				32752UL,
				131056UL,
				524272UL,
				1048560UL,
				4194288UL,
				16777200UL,
				33554416UL,
				134217712UL,
				536870896UL,
				1073741808UL,
				4294475760UL,
				17177780208UL,
				34351351792UL,
				137405399536UL,
				549688705264UL,
				1099445010672UL,
				4397913325680UL,
				17592053915760UL,
				70368614150256UL,
				281474846683248UL,
				281474846683248UL,
				70368614150256UL,
				17592053915760UL,
				4397913325680UL,
				1099445010672UL,
				549688705264UL,
				137405399536UL,
				34351351792UL,
				17177780208UL,
				4294475760UL,
				1073741808UL,
				536870896UL,
				134217712UL,
				33554416UL,
				16777200UL,
				4194288UL,
				1048560UL,
				524272UL,
				131056UL,
				32752UL,
				16368UL,
				4080UL,
				1008UL,
				496UL
			};
			for (int i = 0; i < 48; i++)
			{
				for (int j = 0; j < 48; j++)
				{
					texture2D.SetPixel(i, j, ((array[i] >> j & 1UL) == 1UL) ? color : new Color(0f, 0f, 0f, 0f));
				}
			}
			texture2D.name = "ppp-icon";
			texture2D.Apply();
			return Sprite.Create(texture2D, new Rect(0f, 0f, 48f, 48f), new Vector2(0f, 0f), 1000f);
		}


	}
}
