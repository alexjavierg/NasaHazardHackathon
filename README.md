# AquaShield — Real-Time Hydro-Climatic Hazard, 3D Debris-Flow Simulation & Humanitarian Evacuation Engine

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![NASA Open Science](https://img.shields.io/badge/NASA-Open_Science-0B3D91)](https://science.nasa.gov/open-science/)

## Live Deployment Links

* **Live Interactive Web Platform (Frontend & 3D Map):** [https://aquashield.istpa.tech](https://aquashield.istpa.tech)
* **Live Backend API & Interactive Swagger UI:** [http://13.78.132.29/index.html](http://13.78.132.29/index.html)
* **API Health Check:** [http://13.78.132.29/health](http://13.78.132.29/health)
* **Official GitHub Repository:** [https://github.com/alexjavierg/NasaHazardHackathon](https://github.com/alexjavierg/NasaHazardHackathon)

## Project Overview

**AquaShield** (`NasaHazard.Api`) is a high-performance, database-free geospatial decision-support backend built in **ASP.NET Core (.NET 10)** designed to predict debris flows (*huaicos*), flash floods, and landslide risks anywhere on Earth in real time.

Instead of issuing broad provincial alerts after an event begins, AquaShield crosses **10 years of daily satellite observations (~3,650 days per coordinate)** from **NASA POWER (GPM/MERRA-2)** with **3D Digital Elevation Models (NASADEM/SRTM)** across a 5 km spatial grid, **NASA EONET v3** live natural hazard feeds, and **7-day meteorological forecasts**. It translates complex orbital telemetry into an actionable **Human Life-Saving Plan**: calculating impact lead time in minutes, compass evacuation vectors toward safe elevated ridges outside the ravine channel, inclusive walking times for vulnerable populations (elderly/children), and Sphere-standard humanitarian logistics.

## Scientific Methodology & Key Features

1. **Soil Hydrological Memory & 10-Year Baseline (NASA POWER):** Downloads 10 years of daily precipitation (`PRECTOTCORR`), surface soil wetness (`GWETTOP`), root-zone soil moisture (`GWETPROF`), and maximum temperature (`T2M_MAX`), automatically filtering sensor fill values (`-999`) to compute the local **95th Percentile (P95) Extreme Rain Threshold**.
2. **Non-Parametric Mann-Kendall Trend Validation:** Evaluates decadal hydro-climatic extremes using the Mann-Kendall statistical test (Z-score and two-tailed p-value < 0.001) to verify whether extreme precipitation and soil saturation events follow a statistically significant increasing trend.
3. **5 km 3D Topographic Grid & Flow Vectoring:** Generates a 25-point spatial matrix within a 5 km radius, calculates Haversine distances and slope angles in degrees, traces the 3D debris-flow path from the highest watershed peak to the lowest depositional basin, and identifies an elevated safe evacuation zone outside the main channel.
4. **Live Stress-Test Storm Simulator (`simulatedRainMm`):** Allows emergency planners and citizens to override current rainfall via an interactive UI slider to simulate extreme events (e.g., *El Niño Costero*) over real-time satellite soil saturation.
5. **Humanitarian Infrastructure & Logistics (Overpass OSM + Sphere Standards):** Queries real-world hospitals, clinics, schools, police stations, and fire stations within a 5 km radius and estimates 72-hour potable water requirements (15 L/person/day) and family shelter tents.

## Core API Endpoints

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` / `POST` | `/api/v1/hazard/analyze` | Executes 10-year NASA POWER ingestion, Mann-Kendall trend test, 7-day forecast, 3D terrain flow simulation, and human evacuation plan. |
| `GET` | `/api/v1/hotspots/presets` | Returns 6 high-risk pilot watersheds (Chosica, Punta Hermosa, Piura, Arequipa, Petrópolis, Mocoa) pre-warmed in RAM cache. |
| `GET` | `/api/v1/hotspots/nasa-live-events` | Streams active global severe storms, floods, and landslides from **NASA EONET v3**. |
| `GET` | `/api/v1/emergency/shelters-and-aid` | Retrieves nearby medical/shelter facilities via OpenStreetMap Overpass and computes 72-hour humanitarian aid logistics. |
| `GET` | `/health` | Instantaneous zero-latency liveness probe (`{ "status": "ok" }`). |

## Local Development & Docker Deployment

    git clone https://github.com/alexjavierg/NasaHazardHackathon.git
    cd NasaHazardHackathon
    docker compose up -d --build

## Scientific References & NASA Dataset Citations (APA 7th Edition)

* **NASA POWER (GPM & MERRA-2):** Stackhouse, P. W., Jr., Zhang, T., Westberg, D., Barnett, A. J., Bristow, T., Macpherson, B., & Hoell, J. M. (2024). *NASA Prediction Of Worldwide Energy Resources (POWER) Project Agroclimatology & Renewable Energy Daily API (Version 2.0)* [Data set]. NASA Langley Research Center. https://power.larc.nasa.gov/
* **NASA GPM IMERG:** Huffman, G. J., Stocker, E. F., Bolvin, D. T., Nelkin, E. J., & Tan, J. (2023). *GPM IMERG Final Precipitation L3 1 day 0.1 degree x 0.1 degree V07* [Data set]. NASA Goddard Earth Sciences Data and Information Services Center (GES DISC). https://doi.org/10.5067/GPM/IMERGDF/DAY/07
* **NASA MERRA-2 Soil Moisture:** Global Modeling and Assimilation Office (GMAO). (2015). *MERRA-2 tavg1_2d_lnd_Nx: 2d,1-Hourly,Time-Averaged,Single-Level,Assimilation,Land Surface Diagnostics V5.12.4* [Data set]. NASA GES DISC. https://doi.org/10.5067/RKPHT8KC1Y1T
* **NASA EONET v3:** Earth Observatory Natural Event Tracker (EONET). (2026). *NASA EONET API v3: Curated Near-Real-Time Natural Hazard Events* [Application Programming Interface]. NASA Goddard Space Flight Center. https://eonet.gsfc.nasa.gov/
* **NASADEM / SRTM 3D Topography:** NASA JPL. (2020). *NASADEM Merged DEM Global 1 arc second V001* [Data set]. NASA EOSDIS Land Processes DAAC. https://doi.org/10.5067/MEaSUREs/NASADEM/NASADEM_HGT.001
* **Open-Meteo Elevation & Forecast API:** Zippenfenig, P. (2023). *Open-Meteo.com Weather & Digital Elevation Model API* [Software]. Zenodo. https://doi.org/10.5281/zenodo.7970649
* **OpenStreetMap & Overpass API:** OpenStreetMap Contributors. (2026). *Planet dump & Overpass API geospatial infrastructure data* [Data set]. OpenStreetMap Foundation. https://www.openstreetmap.org/

## License

Distributed under the **Apache License 2.0** in full compliance with **NASA Open Science (SPD-41a)** guidelines. See `LICENSE` for full terms.
