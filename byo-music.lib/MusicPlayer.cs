using System;
using KSA;

namespace MeowSci.ByoMusicLib;

public static class MusicPlayer
{
    public static MusicPlayList? GetPlaylist(string assetId) => ModLibrary.Get<MusicPlayList>(assetId);

    public static void Play(MusicPlayList playlist) => playlist.PlayMusic(out _);
}
