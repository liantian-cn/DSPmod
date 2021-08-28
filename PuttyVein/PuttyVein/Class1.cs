using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using System.Collections;
using System.Collections.Generic;

namespace PuttyVein
{
	[BepInPlugin("me.liantian.plugin.PuttyVein", "PuttyVein", "1.0.0")]
	[BepInProcess("DSPGAME.exe")]
	[BepInDependency("dimava.plugin.Dyson.SquarePatchMod", BepInDependency.DependencyFlags.SoftDependency)]
	public class PuttyVein : BaseUnityPlugin
	{

		public void Awake()
		{
			PuttyVein.VeinLatitude = base.Config.Bind<float>("", "VeinLatitude", 37.5f, "矿物分布维度");



			PuttyVein.more_combustible_mineral = base.Config.Bind<int>("", "more_combustible_mineral", 0, "为未到访过星球刷新更多的可燃物");
			PuttyVein.more_rare_mineral = base.Config.Bind<int>("", "more_rare_mineral", 0, "为未到访过星球刷新更多的更多稀有矿物");
			PuttyVein.more_base_mineral = base.Config.Bind<int>("", "more_base_mineral", 0, "为未到访过星球刷新更多的更多基础矿物");


			PuttyVein.Logger = base.Logger;
			Harmony.CreateAndPatchAll(typeof(PuttyVein));
			// PuttyVein.enabledSprite = PuttyVein.GetSprite(new Color(0f, 1f, 0f));
			// PuttyVein.disabledSprite = PuttyVein.GetSprite(new Color(0.5f, 0.5f, 0.5f));
			PuttyVein.Logger.LogInfo("PuttyVein 初始化");
		}


		[HarmonyPrefix]
		[HarmonyPatch(typeof(GameMain), "Begin")]
		public static  void GameMain_Begin_Prefix()
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

		// Token: 0x04000004 RID: 4
		public new static ManualLogSource Logger;

		// Token: 0x04000006 RID: 6
		// public static bool initialCheckFlag = true;

		// Token: 0x04000007 RID: 7
		// public static bool valueAtLastCheck = true;

		// Token: 0x04000008 RID: 8
		public static RectTransform pppButton = null;

		// Token: 0x04000009 RID: 9
		// public static Sprite enabledSprite;

		// Token: 0x0400000A RID: 10
		// public static Sprite disabledSprite;
		private static ConfigEntry<float> VeinLatitude;

		private static ConfigEntry<int> more_combustible_mineral;
		private static ConfigEntry<int> more_rare_mineral;
		private static ConfigEntry<int> more_base_mineral;




		[HarmonyPrefix]
		[HarmonyPatch(typeof(PlanetAlgorithm), "GenerateVeins")]
		public static bool GenerateVeins(PlanetAlgorithm __instance, bool sketchOnly, PlanetData ___planet, ref Vector3[] ___veinVectors, ref EVeinType[] ___veinVectorTypes, ref int ___veinVectorCount, List<Vector2> ___tmp_vecs)
		{
			lock (___planet)
			{
				ThemeProto themeProto = LDB.themes.Select(___planet.theme);
				if (themeProto != null)
				{
					DotNet35Random dotNet35Random = new DotNet35Random(___planet.seed);
					dotNet35Random.Next();
					dotNet35Random.Next();
					dotNet35Random.Next();
					dotNet35Random.Next();
					int birthSeed = dotNet35Random.Next();
					DotNet35Random dotNet35Random2 = new DotNet35Random(dotNet35Random.Next());
					PlanetRawData data = ___planet.data;
					float num = 2.1f / ___planet.radius;
					VeinProto[] veinProtos = PlanetModelingManager.veinProtos;
					int[] veinModelIndexs = PlanetModelingManager.veinModelIndexs;
					int[] veinModelCounts = PlanetModelingManager.veinModelCounts;
					int[] veinProducts = PlanetModelingManager.veinProducts;
					int[] array = new int[veinProtos.Length];
					float[] array2 = new float[veinProtos.Length];
					float[] array3 = new float[veinProtos.Length];
					if (themeProto.VeinSpot != null)
					{
						Array.Copy(themeProto.VeinSpot, 0, array, 1, Math.Min(themeProto.VeinSpot.Length, array.Length - 1));
					}
					if (themeProto.VeinCount != null)
					{
						Array.Copy(themeProto.VeinCount, 0, array2, 1, Math.Min(themeProto.VeinCount.Length, array2.Length - 1));
					}
					if (themeProto.VeinOpacity != null)
					{
						Array.Copy(themeProto.VeinOpacity, 0, array3, 1, Math.Min(themeProto.VeinOpacity.Length, array3.Length - 1));
					}
					float p = 1f;
					ESpectrType spectr = ___planet.star.spectr;
					EStarType type = ___planet.star.type;
					List<int> base_mineral_list = new List<int>{1,2,3,4,5};
					List<int> rare_mineral_list = new List<int>{9,10,11,12,13,14};
					List<int> combustible_mineral_list = new List<int>{6,7,8};

					for (int i = 1; i <= PuttyVein.more_base_mineral.Value; i++)
					{
						int index = dotNet35Random.Next(base_mineral_list.Count);
						int n = base_mineral_list[index];
						array2[n] = 0.7f;
						array3[n] = 1f;
						array[n]++;
					}

					for (int i = 1; i <= PuttyVein.more_rare_mineral.Value; i++)
					{
						int index = dotNet35Random.Next(rare_mineral_list.Count);
						int n = rare_mineral_list[index];
						array2[n] = 0.7f;
						array3[n] = 1f;
						array[n]++;
					}

					for (int i = 1; i <= PuttyVein.more_combustible_mineral.Value; i++)
					{
						int index = dotNet35Random.Next(combustible_mineral_list.Count);
						int n = combustible_mineral_list[index];
						array2[n] = 0.7f;
						array3[n] = 1f;
						array[n]++;
					}


					if (type == EStarType.MainSeqStar || type == EStarType.GiantStar)
					{
						p = 5f;
					}
					else if (type == EStarType.WhiteDwarf)
					{
						p = 3.5f;
						array[9] = array[9] + 4;
						int num2 = 1;
						while (num2 < 12 && dotNet35Random.NextDouble() < 0.44999998807907104)
						{
							array[9] = array[9] + 2;
							num2++;
						}
						array2[9] = 0.7f;
						array3[9] = 1f;
						array[10]++;
						array[10]++;
						int num3 = 1;
						while (num3 < 12 && dotNet35Random.NextDouble() < 0.44999998807907104)
						{
							array[10]++;
							num3++;
						}
						array2[10] = 0.7f;
						array3[10] = 1f;
						array[12] = array[12] + 2;
						int num4 = 1;
						while (num4 < 12 && dotNet35Random.NextDouble() < 0.5)
						{
							array[12] = array[12] + 2;
							num4++;
						}
						array2[12] = 0.7f;
						array3[12] = 0.3f;
					}
					else if (type == EStarType.NeutronStar)
					{
						p = 4.5f;
						array[14]++;
						int num5 = 1;
						while (num5 < 12 && dotNet35Random.NextDouble() < 0.6499999761581421)
						{
							array[14] = array[14] + 4;
							num5++;
						}
						array2[14] = 0.7f;
						array3[14] = 0.3f;
					}
					else if (type == EStarType.BlackHole)
					{
						p = 5f;
						array[14]++;
						int num6 = 1;
						while (num6 < 12 && dotNet35Random.NextDouble() < 0.6499999761581421)
						{
							array[14] = array[14] + 4;
							num6++;
						}
						array2[14] = 0.7f;
						array3[14] = 0.3f;
					}
					for (int i = 0; i < themeProto.RareVeins.Length; i++)
					{
						int num7 = themeProto.RareVeins[i];
						float num8 = (___planet.star.index == 0) ? themeProto.RareSettings[i * 4] : themeProto.RareSettings[i * 4 + 1];
						float num9 = themeProto.RareSettings[i * 4 + 2];
						float num10 = themeProto.RareSettings[i * 4 + 3];
						float num11 = num10;
						num8 = 1f - Mathf.Pow(1f - num8, p);
						num10 = 1f - Mathf.Pow(1f - num10, p);
						num11 = 1f - Mathf.Pow(1f - num11, p);
						if (dotNet35Random.NextDouble() < (double)num8)
						{
							array[num7]++;
							array2[num7] = num10;
							array3[num7] = num10;
							int num12 = 1;
							while (num12 < 12 && dotNet35Random.NextDouble() < (double)num9)
							{
								array[num7]++;
								num12++;
							}
						}
					}
					bool flag2 = ___planet.galaxy.birthPlanetId == ___planet.id;
					if (flag2 && !sketchOnly)
					{
						___planet.GenBirthPoints(data, birthSeed);
					}
					float num13 = ___planet.star.resourceCoef;
					bool flag3 = GameMain.data.gameDesc.resourceMultiplier >= 99.5f;
					if (flag2)
					{
						num13 *= 0.6666667f;
					}
					float num14 = 1f;
					num14 *= 1.1f;
					Array.Clear(___veinVectors, 0, ___veinVectors.Length);
					Array.Clear(___veinVectorTypes, 0, ___veinVectorTypes.Length);
					___veinVectorCount = 0;
					Vector3 vector;
					if (flag2)
					{
						vector = ___planet.birthPoint;
						vector.Normalize();
						vector *= 0.75f;
					}
					else
					{
						vector.x = (float)dotNet35Random2.NextDouble() * 2f - 1f;
						vector.y = (float)dotNet35Random2.NextDouble() - 0.5f;
						vector.z = (float)dotNet35Random2.NextDouble() * 2f - 1f;
						vector.Normalize();
						vector *= (float)(dotNet35Random2.NextDouble() * 0.4 + 0.2);
					}
					___planet.veinSpotsSketch = array;
					if (!sketchOnly)
					{
						if (flag2)
						{
							___veinVectorTypes[0] = EVeinType.Iron;
							___veinVectors[0] = ___planet.birthResourcePoint0;
							___veinVectorTypes[1] = EVeinType.Copper;
							___veinVectors[1] = ___planet.birthResourcePoint1;
							___veinVectorCount = 2;
						}
						int num15 = 1;
						while (num15 < 15 && ___veinVectorCount < ___veinVectors.Length)
						{
							EVeinType eveinType = (EVeinType)num15;
							int num16 = array[num15];
							if (num16 > 1)
							{
								num16 += dotNet35Random2.Next(-1, 2);
							}
							for (int j = 0; j < num16; j++)
							{
								int num17 = 0;
								Vector3 vector2 = Vector3.zero;
								bool flag4 = false;
								while (num17++ < 200)
								{
									vector2.x = (float)dotNet35Random2.NextDouble() * 2f - 1f;
									vector2.y = (float)dotNet35Random2.NextDouble() * 2f - 1f;
									vector2.z = (float)dotNet35Random2.NextDouble() * 2f - 1f;
									if (eveinType != EVeinType.Oil)
									{
										vector2 += vector;
									}
									vector2.Normalize();
									float num18 = data.QueryHeight(vector2);
									if (num18 >= ___planet.radius && (eveinType != EVeinType.Oil || num18 >= ___planet.radius + 0.5f))
									{
										bool flag5 = false;
										float num19 = (eveinType == EVeinType.Oil) ? 100f : 196f;
										for (int k = 0; k < ___veinVectorCount; k++)
										{
											if ((___veinVectors[k] - vector2).sqrMagnitude < num * num * num19)
											{
												flag5 = true;
												break;
											}
										}
										if (!flag5)
										{
											flag4 = true;
											break;
										}
									}
								}
								if (flag4)
								{
									___veinVectors[___veinVectorCount] = vector2;
									___veinVectorTypes[___veinVectorCount] = eveinType;
									___veinVectorCount++;
									if (___veinVectorCount == ___veinVectors.Length)
									{
										break;
									}
								}
							}
							num15++;
						}
						Array.Clear(___planet.veinAmounts, 0, ___planet.veinAmounts.Length);
						data.veinCursor = 1;
						___planet.veinGroups = new PlanetData.VeinGroup[___veinVectorCount];
						___tmp_vecs.Clear();
						VeinData veinData = default(VeinData);
						for (int l = 0; l < ___veinVectorCount; l++)
						{
							___tmp_vecs.Clear();
							Vector3 normalized = ___veinVectors[l].normalized;
							EVeinType eveinType2 = ___veinVectorTypes[l];
							int num20 = (int)eveinType2;
							Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normalized);
							Vector3 a = rotation * Vector3.right;
							Vector3 a2 = rotation * Vector3.forward;
							___planet.veinGroups[l].type = eveinType2;
							___planet.veinGroups[l].pos = normalized;
							___planet.veinGroups[l].count = 0;
							___planet.veinGroups[l].amount = 0L;
							___tmp_vecs.Add(Vector2.zero);
							int num21 = Mathf.RoundToInt(array2[num20] * (float)dotNet35Random2.Next(20, 25));
							if (eveinType2 == EVeinType.Oil)
							{
								num21 = 1;
							}
							float num22 = array3[num20];
							if (flag2 && l < 2)
							{
								num21 = 6;
								num22 = 0.2f;
							}
							int num23 = 0;
							while (num23++ < 20)
							{
								int count = ___tmp_vecs.Count;
								int num24 = 0;
								while (num24 < count && ___tmp_vecs.Count < num21)
								{
									if (___tmp_vecs[num24].sqrMagnitude <= 36f)
									{
										double num25 = dotNet35Random2.NextDouble() * 3.141592653589793 * 2.0;
										Vector2 vector3 = new Vector2((float)Math.Cos(num25), (float)Math.Sin(num25));
										vector3 += ___tmp_vecs[num24] * 0.2f;
										vector3.Normalize();
										Vector2 vector4 = ___tmp_vecs[num24] + vector3;
										bool flag6 = false;
										for (int m = 0; m < ___tmp_vecs.Count; m++)
										{
											if ((___tmp_vecs[m] - vector4).sqrMagnitude < 0.85f)
											{
												flag6 = true;
												break;
											}
										}
										if (!flag6)
										{
											___tmp_vecs.Add(vector4);
										}
									}
									num24++;
								}
								if (___tmp_vecs.Count >= num21)
								{
									break;
								}
							}
							float num26 = num13;
							if (eveinType2 == EVeinType.Oil)
							{
								num26 = Mathf.Pow(num13, 0.5f);
							}
							int num27 = Mathf.RoundToInt(num22 * 100000f * num26);
							if (num27 < 20)
							{
								num27 = 20;
							}
							int num28 = (num27 < 16000) ? Mathf.FloorToInt((float)num27 * 0.9375f) : 15000;
							int minValue = num27 - num28;
							int maxValue = num27 + num28 + 1;
							for (int n = 0; n < ___tmp_vecs.Count; n++)
							{
								Vector3 b = (___tmp_vecs[n].x * a + ___tmp_vecs[n].y * a2) * num;
								veinData.type = eveinType2;
								veinData.groupIndex = (short)l;
								veinData.modelIndex = (short)dotNet35Random2.Next(veinModelIndexs[num20], veinModelIndexs[num20] + veinModelCounts[num20]);
								veinData.amount = Mathf.RoundToInt((float)dotNet35Random2.Next(minValue, maxValue) * num14);
								if (___planet.veinGroups[l].type != EVeinType.Oil)
								{
									veinData.amount = Mathf.RoundToInt((float)veinData.amount * DSPGame.GameDesc.resourceMultiplier);
								}
								if (veinData.amount < 1)
								{
									veinData.amount = 1;
								}
								if (flag3 && veinData.type != EVeinType.Oil)
								{
									veinData.amount = 1000000000;
								}
								veinData.productId = veinProducts[num20];
								veinData.pos = normalized + b;
								if (veinData.type == EVeinType.Oil)
								{
									veinData.pos = ___planet.aux.RawSnap(veinData.pos);
								}
								veinData.minerCount = 0;
								float num29 = data.QueryHeight(veinData.pos);
								data.EraseVegetableAtPoint(veinData.pos);
								veinData.pos = veinData.pos.normalized * num29;
								if (___planet.waterItemId == 0 || num29 >= ___planet.radius)
								{
									___planet.veinAmounts[(int)eveinType2] += (long)veinData.amount;
									PlanetData.VeinGroup[] veinGroups = ___planet.veinGroups;
									int num30 = l;
									veinGroups[num30].count = veinGroups[num30].count + 1;
									PlanetData.VeinGroup[] veinGroups2 = ___planet.veinGroups;
									int num31 = l;
									veinGroups2[num31].amount = veinGroups2[num31].amount + (long)veinData.amount;
									data.AddVeinData(veinData);
								}
							}
						}
						___tmp_vecs.Clear();
					}
				}
			}
			return false;
		}



	}// public class PuttyVein : BaseUnityPlugin

}
