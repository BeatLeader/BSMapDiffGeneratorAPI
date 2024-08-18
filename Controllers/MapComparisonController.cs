using Microsoft.AspNetCore.Mvc;
using BSMapDiffGenerator;
using BSMapDiffGenerator.Models;
using Newtonsoft.Json;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using System.Diagnostics;
using beatleader_parser;
using System.Text;

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

            var sb = new StringBuilder();

            int addedCount = diff.Count(x => x.Type == DiffType.Added);
            int removedCount = diff.Count(x => x.Type == DiffType.Removed);
            int modifiedCount = diff.Count(x => x.Type == DiffType.Modified);

            sb.AppendLine($"Changes --- Total: {diff.Count} | Added: {addedCount} | Removed: {removedCount} | Modified: {modifiedCount}");

            diff.Sort((x, y) => x.Object.Beats.CompareTo(y.Object.Beats));

            foreach (var entry in diff)
            {
                if (entry.Object is BeatmapColorGridObject cobj)
                {
                    string color = cobj.Color == 0 ? "Red" : "Blue";
                    string baseText = $"{cobj.Beats} at x{cobj.x} y{cobj.y} ({color} in {entry.CollectionType})\n";

                    switch (entry.Type)
                    {
                        case DiffType.Added:
                            sb.Append($"+ Added    {baseText}");
                            break;
                        case DiffType.Removed:
                            sb.Append($"- Removed  {baseText}");
                            break;
                        case DiffType.Modified:
                            sb.Append($"/ Modified {baseText}");
                            break;
                    }
                }
                else if (entry.Object is BeatmapGridObject obj)
                {
                    string baseText = $"{obj.Beats} at x {obj.x} y {obj.y} (in {entry.CollectionType})\n";

                    switch (entry.Type)
                    {
                        case DiffType.Added:
                            sb.Append($"+ Added    {baseText}");
                            break;
                        case DiffType.Removed:
                            sb.Append($"- Removed  {baseText}");
                            break;
                        case DiffType.Modified:
                            sb.Append($"/ Modified {baseText}");
                            break;
                    }
                }
                else
                {
                    string baseText = $"{entry.Object.Beats} (in {entry.CollectionType})\n";

                    switch (entry.Type)
                    {
                        case DiffType.Added:
                            sb.Append($"+ Added    {baseText}");
                            break;
                        case DiffType.Removed:
                            sb.Append($"- Removed  {baseText}");
                            break;
                        case DiffType.Modified:
                            sb.Append($"/ Modified {baseText}");
                            break;
                    }
                }
            }

            string description = sb.ToString();

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