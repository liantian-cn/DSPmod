using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using UnityEngine;


namespace BetterStarPlanets
{
    [BepInPlugin("me.liantian.plugin.BetterStarPlanets", "BetterStarPlanets", "1.0")]
    public class BetterStarPlanets : BaseUnityPlugin
    {
		public new static ManualLogSource Logger;
		void Start()
		{
			BetterStarPlanets.Logger = base.Logger;
			Harmony.CreateAndPatchAll(typeof(BetterStarPlanets));
		}



		[HarmonyPrefix]
		[HarmonyPatch(typeof(StarGen), "CreateStarPlanets")]
		public static bool CreateStarPlanetsPatch(GalaxyData galaxy, StarData star, GameDesc gameDesc)
		{
			//__result = CreateGalaxy(gameDesc);
			CreateStarPlanets(galaxy, star, gameDesc);
			return false;
		}

		public static void SetPGas(int index, double value)
		{
			var pGas = Traverse.Create(typeof(StarGen)).Field("pGas").GetValue<double[]>();
			pGas[index] = value;
			Traverse.Create(typeof(StarGen)).Field("pGas").SetValue(pGas);
		}
		public static void CreateStarPlanets(GalaxyData galaxy, StarData star, GameDesc gameDesc)
		{
			DotNet35Random dotNet35Random = new DotNet35Random(star.seed);
			dotNet35Random.Next();
			dotNet35Random.Next();
			dotNet35Random.Next();
			DotNet35Random dotNet35Random2 = new DotNet35Random(dotNet35Random.Next());
			double num = dotNet35Random2.NextDouble();
			double num2 = dotNet35Random2.NextDouble();
			double num3 = dotNet35Random2.NextDouble();
			double num4 = dotNet35Random2.NextDouble();
			double num5 = dotNet35Random2.NextDouble();
			double num6 = dotNet35Random2.NextDouble() * 0.2 + 0.9;
			double num7 = dotNet35Random2.NextDouble() * 0.2 + 0.9;
			if (star.type == EStarType.BlackHole)
			{
				star.planetCount = 1;
				star.planets = new PlanetData[star.planetCount];
				int info_seed = dotNet35Random2.Next();
				int gen_seed = dotNet35Random2.Next();
				star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, 3, 1, false, info_seed, gen_seed);
			}
			else if (star.type == EStarType.NeutronStar)
			{
				star.planetCount = 1;
				star.planets = new PlanetData[star.planetCount];
				int info_seed2 = dotNet35Random2.Next();
				int gen_seed2 = dotNet35Random2.Next();
				star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, 3, 1, false, info_seed2, gen_seed2);
			}
			else if (star.type == EStarType.WhiteDwarf)
			{
				star.planetCount = 2;
				star.planets = new PlanetData[star.planetCount];
				if (num2 < 0.30000001192092896)
				{
					int info_seed3 = dotNet35Random2.Next();
					int gen_seed3 = dotNet35Random2.Next();
					star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, 3, 1, false, info_seed3, gen_seed3);
					info_seed3 = dotNet35Random2.Next();
					gen_seed3 = dotNet35Random2.Next();
					star.planets[1] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 1, 0, 4, 2, false, info_seed3, gen_seed3);
				}
				else
				{
					int info_seed4 = dotNet35Random2.Next();
					int gen_seed4 = dotNet35Random2.Next();
					star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, 4, 1, true, info_seed4, gen_seed4);
					info_seed4 = dotNet35Random2.Next();
					gen_seed4 = dotNet35Random2.Next();
					star.planets[1] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 1, 1, 1, 1, false, info_seed4, gen_seed4);
				}
			}
			else if (star.type == EStarType.GiantStar)
			{
				star.planetCount = 3;
				star.planets = new PlanetData[star.planetCount];
				if (num2 < 0.15000000596046448)
				{
					int info_seed5 = dotNet35Random2.Next();
					int gen_seed5 = dotNet35Random2.Next();
					star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, (num3 > 0.5) ? 3 : 2, 1, false, info_seed5, gen_seed5);
					info_seed5 = dotNet35Random2.Next();
					gen_seed5 = dotNet35Random2.Next();
					star.planets[1] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 1, 0, (num3 > 0.5) ? 4 : 3, 2, false, info_seed5, gen_seed5);
					info_seed5 = dotNet35Random2.Next();
					gen_seed5 = dotNet35Random2.Next();
					star.planets[2] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 2, 0, (num3 > 0.5) ? 5 : 4, 3, false, info_seed5, gen_seed5);
				}
				else if (num2 < 0.75)
				{
					int info_seed6 = dotNet35Random2.Next();
					int gen_seed6 = dotNet35Random2.Next();
					star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, (num3 > 0.5) ? 3 : 2, 1, false, info_seed6, gen_seed6);
					info_seed6 = dotNet35Random2.Next();
					gen_seed6 = dotNet35Random2.Next();
					star.planets[1] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 1, 0, 4, 2, true, info_seed6, gen_seed6);
					info_seed6 = dotNet35Random2.Next();
					gen_seed6 = dotNet35Random2.Next();
					star.planets[2] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 2, 2, 1, 1, false, info_seed6, gen_seed6);
				}
				else
				{
					int info_seed7 = dotNet35Random2.Next();
					int gen_seed7 = dotNet35Random2.Next();
					star.planets[0] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 0, 0, (num3 > 0.5) ? 4 : 3, 1, true, info_seed7, gen_seed7);
					info_seed7 = dotNet35Random2.Next();
					gen_seed7 = dotNet35Random2.Next();
					star.planets[1] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 1, 1, 1, 1, false, info_seed7, gen_seed7);
					info_seed7 = dotNet35Random2.Next();
					gen_seed7 = dotNet35Random2.Next();
					star.planets[2] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, 2, 1, 2, 2, false, info_seed7, gen_seed7);
				}
			}
			else
			{

				if (star.index == 0)
				{
					star.planetCount = 4;
					SetPGas(0, 0);
					SetPGas(1, 0);
					SetPGas(2, 0);
				}
				else if (star.spectr == ESpectrType.M || star.spectr == ESpectrType.K || star.spectr == ESpectrType.G || star.spectr == ESpectrType.F || star.spectr == ESpectrType.A || star.spectr == ESpectrType.B || star.spectr == ESpectrType.O)
				{
					star.planetCount = 8;
					SetPGas(0, 0.1);
					SetPGas(1, 0.2);
					SetPGas(2, 0.3);
					SetPGas(3, 0.4);
					SetPGas(4, 0.1);
					SetPGas(5, 0.2);
					SetPGas(6, 0.3);
					SetPGas(7, 0.4);

				}
				else
				{
					star.planetCount = 1;
				}
				star.planets = new PlanetData[star.planetCount];
				int num8 = 0;
				int num9 = 0;
				int num10 = 0;
				int num11 = 1;
				for (int i = 0; i < star.planetCount; i++)
				{
					int info_seed8 = dotNet35Random2.Next();
					int gen_seed8 = dotNet35Random2.Next();
					double num12 = dotNet35Random2.NextDouble();
					double num13 = dotNet35Random2.NextDouble();
					bool flag = false;
					if (num10 == 0)
					{
						num8++;
						if (i < star.planetCount - 1 && num12 < Traverse.Create(typeof(StarGen)).Field("pGas").GetValue<double[]>()[i])
						{
							flag = true;
							if (num11 < 3)
							{
								num11 = 3;
							}
						}
						while (star.index != 0 || num11 != 3)
						{
							int num14 = star.planetCount - i;
							int num15 = 9 - num11;
							if (num15 <= num14)
							{
								goto IL_C17;
							}
							float num16 = (float)num14 / (float)num15;
							if (num11 > 3)
							{
								num16 = Mathf.Lerp(num16, 1f, 0.45f) + 0.01f;
							}
							else
							{
								num16 = Mathf.Lerp(num16, 1f, 0.15f) + 0.01f;
							}
							if (dotNet35Random2.NextDouble() < (double)num16)
							{
								goto IL_C17;
							}
							num11++;
						}
						flag = true;
					}
					else
					{
						num9++;
						flag = false;
					}
				IL_C17:
					star.planets[i] = PlanetGen.CreatePlanet(galaxy, star, gameDesc, i, num10, (num10 == 0) ? num11 : num9, (num10 == 0) ? num8 : num9, flag, info_seed8, gen_seed8);
					num11++;
					if (flag)
					{
						num10 = num8;
						num9 = 0;
					}
					if (num9 >= 1 && num13 < 0.8)
					{
						num10 = 0;
						num9 = 0;
					}
				}
			}
			int num17 = 0;
			int num18 = 0;
			int num19 = 0;
			for (int j = 0; j < star.planetCount; j++)
			{
				if (star.planets[j].type == EPlanetType.Gas)
				{
					num17 = star.planets[j].orbitIndex;
					break;
				}
			}
			for (int k = 0; k < star.planetCount; k++)
			{
				if (star.planets[k].orbitAround == 0)
				{
					num18 = star.planets[k].orbitIndex;
				}
			}
			if (num17 > 0)
			{
				int num20 = num17 - 1;
				bool flag2 = true;
				for (int l = 0; l < star.planetCount; l++)
				{
					if (star.planets[l].orbitAround == 0 && star.planets[l].orbitIndex == num17 - 1)
					{
						flag2 = false;
						break;
					}
				}
				if (flag2 && num4 < 0.2 + (double)num20 * 0.2)
				{
					num19 = num20;
				}
			}
			int num21;
			if (num5 < 0.2)
			{
				num21 = num18 + 3;
			}
			else if (num5 < 0.4)
			{
				num21 = num18 + 2;
			}
			else if (num5 < 0.8)
			{
				num21 = num18 + 1;
			}
			else
			{
				num21 = 0;
			}
			if (num21 != 0 && num21 < 5)
			{
				num21 = 5;
			}
			star.asterBelt1OrbitIndex = (float)num19;
			star.asterBelt2OrbitIndex = (float)num21;
			if (num19 > 0)
			{
				star.asterBelt1Radius = StarGen.orbitRadius[num19] * (float)num6 * star.orbitScaler;
			}
			if (num21 > 0)
			{
				star.asterBelt2Radius = StarGen.orbitRadius[num21] * (float)num7 * star.orbitScaler;
			}
		}


	}
}
