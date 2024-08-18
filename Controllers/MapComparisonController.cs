using Microsoft.AspNetCore.Mvc;
using BSMapDiffGenerator;
using BSMapDiffGenerator.Models;
using Newtonsoft.Json;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using System.Diagnostics;
using beatleader_parser;

namespace BSMapDiffGeneratorAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MapComparisonController : ControllerBase
    {
        private readonly ILogger<MapComparisonController> _logger;

        public MapComparisonController(ILogger<MapComparisonController> logger)
        {
            _logger = logger;
        }

        [NonAction]
        public (List<DiffEntry>?, string?) MapsCompare(string oldMapLink, string newMapLink, string diffToCompare, string charToCompare, bool compareLights) {
            Parse parser = new();
            BeatmapV3? oldMap = parser.TryDownloadLink(oldMapLink).FirstOrDefault();
            BeatmapV3? newMap = parser.TryDownloadLink(newMapLink).FirstOrDefault();

            if (oldMap == null || newMap == null) {
                return (null, null);
            }

            var diff = MapDiffGenerator.GenerateDifficultyDiff(newMap.Difficulties.First(x => x.Difficulty == diffToCompare && x.Characteristic == charToCompare).Data, oldMap.Difficulties.First(x => x.Difficulty == diffToCompare && x.Characteristic == charToCompare).Data);

            if (!compareLights) {
                CollectionType[] lightCollections = [CollectionType.Lights, CollectionType.ColorBoostBeatmapEvents, CollectionType.LightColorEventBoxGroups, CollectionType.LightRotationEventBoxGroups, CollectionType.LightTranslationEventBoxGroups];
                diff = diff.Where(d => !lightCollections.Contains(d.CollectionType)).ToList();
            }

            var description = "";

            description += $"Changes --- Total: {diff.Count} | Added: {diff.Count(x => x.Type == DiffType.Added)} | Removed: {diff.Count(x => x.Type == DiffType.Removed)} | Modified: {diff.Count(x => x.Type == DiffType.Modified)}\n";

            diff.Sort((x, y) => x.Object.Beats.CompareTo(y.Object.Beats));

            foreach (var entry in diff)
            {
                if (entry.Object is BeatmapColorGridObject cobj)
                {
                    string color = cobj.Color == 0 ? "Red" : "Blue";

                    switch (entry.Type)
                    {
                        case (DiffType)0:
                            description += $"+ Added    {cobj.Beats} at x{cobj.x} y{cobj.y} ({color} in {entry.CollectionType})\n";
                            break;
                        case (DiffType)1:
                            description += $"- Removed  {cobj.Beats} at x{cobj.x} y{cobj.y} ({color} in {entry.CollectionType})\n";
                            break;
                        case (DiffType)2:
                            description += $"/ Modified {cobj.Beats} at x{cobj.x} y{cobj.y} ({color} in {entry.CollectionType})\n";
                            break;
                        default: break;
                    }
                }
                else
                {
                    switch (entry.Type)
                    {
                        case (DiffType)0:
                            if (entry.Object is BeatmapGridObject obj) { 
                                description += $"+ Added    {obj.Beats} at x {obj.x} y {obj.y} (in {entry.CollectionType})\n";
                            } else { 
                                description += $"+ Added    {entry.Object.Beats} (in {entry.CollectionType})\n";
                            }
                            break;
                        case (DiffType)1:
                            if (entry.Object is BeatmapGridObject obj2) {
                                description += $"- Removed  {obj2.Beats} at x {obj2.x} y {obj2.y} (in {entry.CollectionType})\n";
                            } else {
                                description += $"- Removed  {entry.Object.Beats} (in {entry.CollectionType})\n";
                            }
                            break;
                        case (DiffType)2:
                            if (entry.Object is BeatmapGridObject obj3) {
                                description += $"/ Modified {obj3.Beats} at x {obj3.x} y {obj3.y} (in {entry.CollectionType})\n";
                            } else {
                                description += $"/ Modified {entry.Object.Beats} (in {entry.CollectionType})\n";
                            }
                            break;
                        default: break;
                    }
                }
            }

            return (diff, description);
        }


        [HttpGet("~/mapscompare/json")]
        public async Task<ActionResult> MapsCompareJson(
            [FromQuery] string oldMapLink, 
            [FromQuery] string newMapLink,
            [FromQuery] string diffToCompare = "ExpertPlus",
            [FromQuery] string charToCompare = "Standard",
            [FromQuery] bool compareLights = true) {

            (var diff, _) = MapsCompare(oldMapLink, newMapLink, diffToCompare, charToCompare, compareLights);

            if (diff == null) {
                return BadRequest("Can't parse maps");
            }

            return Ok(JsonConvert.SerializeObject(diff, Formatting.Indented));
        }   


        [HttpGet("~/mapscompare/text")]
        public async Task<ActionResult> MapsCompareText(
            [FromQuery] string oldMapLink, 
            [FromQuery] string newMapLink,
            [FromQuery] string diffToCompare = "ExpertPlus",
            [FromQuery] string charToCompare = "Standard",
            [FromQuery] bool compareLights = true) {

            (_, var description) = MapsCompare(oldMapLink, newMapLink, diffToCompare, charToCompare, compareLights);

            if (description == null) {
                return BadRequest("Can't parse maps");
            }

            return Ok(description);
        } 
    }
}