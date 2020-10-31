using Clypto.Server.Data;
using Clypto.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Serilog;
using Clypto.Server.Services;
using System.IO;

namespace Clypto.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ClipController : ControllerBase
    {
        private readonly IClipRepository _clipRepo;
        private readonly IMapper _mapper;
        private readonly AzureBlobService _blobService;

        public ClipController(IClipRepository clipRepo, IMapper mapper, AzureBlobService blobService)
        {
            _clipRepo = clipRepo;
            _blobService = blobService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                var clips = _clipRepo.Get().AsEnumerable();
                return Ok(_mapper.Map<IEnumerable<ClipDTO>>(clips));
            }
            catch (Exception ex)
            {
                Log.Error("Error occured executing get on all clips: {error}", ex.Message);
                return StatusCode(500);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Get(params string[] tags)
        {
            try
            {
                var clips = (await _clipRepo.GetClipsByTags(tags)).AsEnumerable();
                return Ok(_mapper.Map<IEnumerable<ClipDTO>>(clips));
            }
            catch (Exception ex)
            {
                Log.Error("Error occured executing get on clips by tags: {error}", ex.Message);
                return StatusCode(500);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Get(string clipId)
        {
            var dbClip = _clipRepo.Get(clipId);
            if (dbClip == null)
            {
                return StatusCode(404);
            }

            await _blobService.EnsureClipDownloadedAsync(dbClip);

            string clipFullPath = Path.Combine(Directory.GetCurrentDirectory(), "clips", dbClip.FileName);

            var buffer = new byte[0];
            using (var fs = new FileStream(clipFullPath, FileMode.Open, FileAccess.Read))
            {
                var br = new BinaryReader(fs);
                long numBytes = new FileInfo(clipFullPath).Length;
                buffer = br.ReadBytes((int)numBytes);
            }

            return File(buffer, "audio/mpeg", dbClip.FileName);
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ClipDTO clip)
        {
            try
            {
                // Do stuff
                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error("Error occured adding new clip: {error}", ex.Message);
                return StatusCode(500);
            }
        }
    }
}
