using System;
using System.Collections.Generic;

/// <summary>Exact UI-text translations; identifiers, numbers and unknown text pass through unchanged.</summary>
public static class LocalizeCatalog
{
    private static readonly Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> vietnamese = new Dictionary<string, string>(StringComparer.Ordinal);

    static LocalizeCatalog()
    {
        // Navigation, common actions and headings. Language choices retain their native names.
        Add("CỬA HÀNG", "SHOP");
        Add("Cửa hàng", "Shop");
        Add("NHIỆM VỤ", "MISSIONS");
        Add("Nhiệm vụ", "Missions");
        Add("NHIỆM\nVỤ", "DAILY\nMISSIONS");
        Add("NHIỆM\nVỤ ✓", "DAILY\nMISSIONS ✓");
        Add("TRANG CHỦ", "HOME");
        Add("Trang chủ", "Home");
        Add("THÀNH TÍCH", "ACHIEVEMENTS");
        Add("Thành tích", "Achievements");
        Add("NHÂN VẬT", "CHARACTERS");
        Add("Nhân vật", "Characters");
        Add("CHƠI", "PLAY");
        Add("Chơi", "Play");
        Add("CHƠI NGAY", "PLAY NOW");
        Add("Chơi ngay", "Play now");
        Add("CHƠI TIẾP", "KEEP PLAYING");
        Add("Chơi tiếp", "Keep playing");
        Add("CHƠI LẠI", "RETRY");
        Add("Chơi lại", "Retry");
        Add("TIẾP TỤC", "CONTINUE");
        Add("Tiếp tục", "Continue");
        Add("VỀ HOME", "BACK HOME");
        Add("Về Home", "Back home");
        Add("VỀ TRANG CHỦ", "BACK TO HOME");
        Add("Về trang chủ", "Back to home");
        Add("ĐÓNG", "CLOSE");
        Add("Đóng", "Close");
        Add("HỦY", "CANCEL");
        Add("Hủy", "Cancel");
        Add("ĐỒNG Ý", "OK");
        Add("Đồng ý", "Okay");
        Add("ĐÃ HIỂU", "GOT IT");
        Add("Đã hiểu", "Got it");
        Add("ĐỂ SAU", "LATER");
        Add("Để sau", "Later");
        Add("NHẬN", "CLAIM");
        Add("Nhận", "Claim");
        Add("NHẬN THƯỞNG", "CLAIM REWARD");
        Add("Nhận thưởng", "Claim reward");
        Add("ĐÃ NHẬN", "CLAIMED");
        Add("Đã nhận", "Claimed");
        Add("MỞ KHÓA", "UNLOCK");
        Add("Mở khóa", "Unlock");
        Add("ĐÃ MỞ KHÓA", "UNLOCKED");
        Add("CHƯA MỞ KHÓA", "LOCKED");
        Add("TẠM DỪNG", "PAUSED");
        Add("ĐANG TẢI", "LOADING");
        Add("Đang tải", "Loading");
        Add("Đang tải:", "Loading:");

        // Settings and support.
        Add("CÀI ĐẶT", "SETTINGS");
        Add("Cài đặt", "Settings");
        Add("Nhạc nền", "Music");
        Add("NHẠC NỀN", "MUSIC");
        Add("Rung", "Vibration");
        Add("RUNG", "VIBRATION");
        Add("Âm thanh", "Sound");
        Add("ÂM THANH", "SOUND");
        Add("Hiệu ứng âm thanh", "Sound effects");
        Add("Ngôn ngữ", "Language");
        Add("NGÔN NGỮ", "LANGUAGE");
        Add("Nhập code", "Enter code");
        Add("Nhập code...", "Enter code...");
        Add("Chế độ người mù", "Blind mode");
        Add("Hỗ trợ biểu tượng", "Icon support");
        Add("TRỢ GIÚP & HỖ TRỢ", "HELP & SUPPORT");
        Add("Trợ giúp & hỗ trợ", "Help & support");
        Add("KHÔI PHỤC GIAO DỊCH", "RESTORE PURCHASES");
        Add("XÓA QUẢNG CÁO", "REMOVE ADS");
        Add("BẬT", "ON");
        Add("TẮT", "OFF");

        // Difficulty, characters and their purchase feedback.
        Add("CỰC KỲ DỄ", "VERY EASY");
        Add("KHÁ DỄ", "FAIRLY EASY");
        Add("DỄ", "EASY");
        Add("BÌNH THƯỜNG", "NORMAL");
        Add("KHÁ KHÓ", "FAIRLY HARD");
        Add("KHÓ", "HARD");
        Add("SIÊU KHÓ", "VERY HARD");
        Add("CỰC KỲ KHÓ", "EXTREMELY HARD");
        Add("BÉ HÚT BỤI", "VACUUM BOT");
        Add("BÉ HÚT BỤI XANH", "BLUE VACUUM BOT");
        Add("BÉ HÚT BỤI HỒNG", "PINK VACUUM BOT");
        Add("BÉ HÚT BỤI LÁ", "GREEN VACUUM BOT");
        Add("ĐANG SỬ DỤNG", "EQUIPPED");
        Add("SỬ DỤNG", "EQUIP");
        Add("MUA 200", "BUY 200");
        Add("CHƯA ĐỦ VÀNG", "NOT ENOUGH GOLD");
        Add("NHÂN VẬT ĐANG KHÓA", "CHARACTER LOCKED");
        Add("Cần 200 vàng để mở khóa nhân vật xanh.", "You need 200 gold to unlock the blue character.");
        Add("Nhân vật này sẽ được mở khóa qua sự kiện.", "This character will be unlocked through events.");

        // Daily missions and login rewards, including the currently authored six tasks.
        Add("NHIỆM VỤ HẰNG NGÀY", "DAILY MISSIONS");
        Add("NHIỆM VỤ HÀNG NGÀY", "DAILY TASKS");
        Add("NHIỆM VỤ HÔM NAY", "TODAY'S MISSIONS");
        Add("PHẦN THƯỞNG\nĐĂNG NHẬP HÀNG NGÀY", "DAILY\nLOGIN REWARDS");
        Add("PHẦN THƯỞNG ĐĂNG NHẬP HÀNG NGÀY", "DAILY LOGIN REWARDS");
        Add("THƯỞNG ĐĂNG NHẬP", "LOGIN REWARDS");
        Add("ĐIỂM DANH HẰNG NGÀY", "DAILY CHECK-IN");
        Add("Phá 50 khối", "Break 50 blocks");
        Add("Phá 50 khối trong bất kỳ chế độ nào", "Break 50 blocks in any mode");
        Add("Chơi 3 ván", "Play 3 rounds");
        Add("Chơi 3 ván bất kỳ", "Play any 3 rounds");
        Add("Kết thúc 3 ván (thắng hoặc thua)", "Finish 3 rounds (win or lose)");
        Add("Dùng 5 booster", "Use 5 boosters");
        Add("Sử dụng 5 booster trong game", "Use 5 boosters in the game");
        Add("Thắng 5 ván", "Win 5 rounds");
        Add("Thắng tổng cộng 5 ván trong ngày", "Win a total of 5 rounds today");
        Add("Chuỗi thắng 3", "Win streak of 3");
        Add("Thắng 3 ván liên tiếp trong ngày", "Win 3 rounds in a row today");
        Add("Phá 1.000 khối", "Break 1,000 blocks");
        Add("Phá tổng cộng 1.000 khối trong ngày", "Break a total of 1,000 blocks today");
        Add("Bạn đã nhận quà hôm nay!", "You have claimed today's reward!");
        Add("ĐÃ NHẬN THƯỞNG", "REWARD CLAIMED");
        Add("ĐÃ NHẬN THƯỞNG!", "REWARD CLAIMED!");
        for (int day = 1; day <= 7; day++)
        {
            Add("Ngày " + day, "Day " + day);
            Add("NGÀY " + day, "DAY " + day);
        }

        // Lifetime achievement definitions.
        Add("Phá 100 khối", "Break 100 blocks");
        Add("Thắng 3 ván", "Win 3 rounds");
        Add("Thắng 20 ván", "Win 20 rounds");
        Add("Dùng 25 booster", "Use 25 boosters");
        Add("Phá 5.000 khối", "Break 5,000 blocks");
        Add("Thắng 100 ván", "Win 100 rounds");
        Add("Dùng 100 booster", "Use 100 boosters");
        Add("Phá 20.000 khối", "Break 20,000 blocks");
        Add("VUỐT LÊN/XUỐNG ĐỂ XEM ĐỦ 10 THẺ", "SWIPE UP/DOWN TO VIEW ALL 10 CARDS");

        // Shop products and offers; the values are display text rather than product IDs.
        Add("VÀNG", "GOLD");
        Add("Vàng", "Gold");
        Add("TIM", "HEARTS");
        Add("Tim", "Hearts");
        Add("500 VÀNG", "500 GOLD");
        Add("200 VÀNG", "200 GOLD");
        Add("1.200 VÀNG", "1.200 GOLD");
        Add("2.500 VÀNG", "2.500 GOLD");
        Add("6.500 VÀNG", "6.500 GOLD");
        Add("14.000 VÀNG", "14.000 GOLD");
        Add("30.000 VÀNG", "30.000 GOLD");
        Add("5 TIM", "5 HEARTS");
        Add("20 TIM", "20 HEARTS");
        Add("KÍNH LÚP", "MAGNIFIER");
        Add("Kính lúp", "Magnifier");
        Add("KÉO", "SCISSORS");
        Add("Kéo", "Scissors");
        Add("XÁO TRỘN", "SHUFFLE");
        Add("Xáo trộn", "Shuffle");
        Add("LẤY VẬT THỂ", "PICK UP");
        Add("Lấy vật thể", "Pick up");
        Add("GÓI KHỞI ĐẦU", "STARTER PACK");
        Add("GÓI NHỎ", "SMALL PACK");
        Add("GÓI TRUNG ĐỊNH", "MEDIUM PACK");
        Add("GÓI LỚN", "LARGE PACK");
        Add("GÓI ĐẶC BIỆT", "SPECIAL PACK");
        Add("30 PHÚT", "30 MIN");
        Add("1 GIỜ", "1 HOUR");
        Add("2 GIỜ", "2 HOURS");
        Add("30 phút", "30 min");
        Add("1 giờ", "1 hour");
        Add("2 giờ", "2 hours");
        Add("ĐANG TẢI GIÁ", "LOADING PRICE");
        Add("MUA THÀNH CÔNG", "PURCHASE SUCCESSFUL");
        Add("KHÔNG ĐỦ VÀNG", "INSUFFICIENT GOLD");
        Add("CHƯA THỂ MUA", "PURCHASE UNAVAILABLE");
        Add("VUI LÒNG THỬ LẠI", "PLEASE TRY AGAIN");
        Add("ĐANG CHỜ DUYỆT", "AWAITING APPROVAL");
        Add("Vật phẩm đã được thêm vào kho", "Items added to your inventory");
        Add("Đã nhận vào kho của bạn", "Added to your inventory");
        Add("Chưa lưu được vật phẩm. Vàng đã được hoàn lại.", "Could not save the item. Your gold has been refunded.");
        Add("Chưa lưu được tim. Vàng đã được hoàn lại, vui lòng thử lại.", "Could not save the hearts. Your gold has been refunded. Please try again.");
        Add("Chưa thể hoàn tất giao dịch. Vui lòng thử lại sau.", "Could not complete the purchase. Please try again later.");
        Add("Cửa hàng chưa sẵn sàng. Vui lòng thử lại sau.", "The shop is not ready. Please try again later.");
        Add("Giao dịch chưa hoàn tất.", "The purchase is incomplete.");
        Add("Giao dịch đang chờ phê duyệt. Vật phẩm sẽ được nhận sau khi thanh toán hoàn tất.",
            "The purchase is awaiting approval. You will receive the items after payment is complete.");
        Add("Không thể kết nối cửa hàng. Vui lòng thử lại.", "Cannot connect to the store. Please try again.");
        Add("Mất kết nối cửa hàng. Vui lòng thử lại.", "Store connection lost. Please try again.");
        Add("Chưa xác minh được giao dịch. Vui lòng thử lại sau.", "The transaction has not been verified yet. Please try again later.");
        Add("Không thể xác minh giao dịch. Chưa cộng vật phẩm.", "Cannot verify this transaction. No items have been added.");
        Add("Đang tải dữ liệu người chơi. Vui lòng mở lại cửa hàng.", "Player data is loading. Please reopen the shop.");
        Add("Đang xử lý giao dịch. Vật phẩm sẽ được khôi phục khi mở lại trò chơi.",
            "Your transaction is being processed. Items will be restored when you reopen the game.");
        Add("Đã hủy mua gói.", "Pack purchase cancelled.");
        Add("Mua gói chưa thành công. Vui lòng thử lại.", "Pack purchase failed. Please try again.");
        Add("Vui lòng thử lại sau.", "Please try again later.");
        Add("Bạn cần 200 vàng để mua kính lúp.", "You need 200 gold to buy a magnifier.");
        Add("Bạn cần 200 vàng để mua kéo.", "You need 200 gold to buy scissors.");
        Add("Bạn cần 200 vàng để mua xáo trộn.", "You need 200 gold to buy a shuffle.");
        Add("Bạn cần 200 vàng để mua lấy vật thể.", "You need 200 gold to buy a pick-up booster.");
        Add("Kính lúp ×1", "Magnifier ×1");
        Add("Kéo ×1", "Scissors ×1");
        Add("Xáo trộn ×1", "Shuffle ×1");
        Add("Lấy vật thể ×1", "Pick up ×1");
        Add("Kính lúp, Kéo, Xáo trộn, Lấy vật thể", "Magnifier, Scissors, Shuffle, Pick up");

        // Hearts, results and round-exit confirmation.
        Add("ĐẦY", "FULL");
        Add("HẾT TIM", "OUT OF HEARTS");
        Add("Bạn đã hết tim!", "You're out of hearts!");
        Add("BẠN ĐÃ HẾT TIM!", "YOU'RE OUT OF HEARTS!");
        Add("Bạn đã hết tim.", "You are out of hearts.");
        Add("Bạn đã hết tim", "You are out of hearts");
        Add("Tới shop", "Go to shop");
        Add("TỚI SHOP", "GO TO SHOP");
        Add("Chờ tiếp", "Wait");
        Add("CHỜ TIẾP", "WAIT");
        Add("Tim vô hạn", "Unlimited hearts");
        Add("TIM VÔ HẠN", "UNLIMITED HEARTS");
        Add("Tim vô hạn đang hoạt động.", "Unlimited hearts are active.");
        Add("Bạn đang có tim vô hạn.\nThua, chơi lại hoặc thoát ván sẽ không mất tim.",
            "You have unlimited hearts.\nLosing, retrying or leaving will not cost a heart.");
        Add("Tim đã đầy!", "Hearts are full!");
        Add("Mỗi tim hồi sau 15 phút.", "One heart refills every 15 minutes.");
        Add("Bạn sẽ mất 1 tim", "You will lose 1 heart");
        Add("Bạn sẽ mất 1 tim.", "You will lose 1 heart.");
        Add("Bạn cần thêm tim để chơi lại.", "You need more hearts to retry.");
        Add("Bạn sẽ mất 1 tim.\nBạn cần thêm tim để chơi lại.", "You will lose 1 heart.\nYou need more hearts to retry.");
        Add("Ván đã kết thúc, không trừ thêm tim.", "The round has ended. No extra heart will be spent.");
        Add("THOÁT VÁN?", "LEAVE ROUND?");
        Add("THOÁT VÁN", "LEAVE ROUND");
        Add("Ở LẠI", "STAY");
        Add("HẾT CHỖ TRỐNG!", "NO SPACE LEFT!");
        Add("Thử lại để chinh phục màn chơi!", "Try again to beat this level!");
        Add("CHIẾN THẮNG!", "VICTORY!");
        Add("MÀN TIẾP THEO", "NEXT LEVELS");
        Add("NHẬN X2", "CLAIM X2");
        Add("XEM QUẢNG CÁO", "WATCH AD");
        Add("NHẬN & TIẾP", "CLAIM & NEXT");
        Add("ĐANG CHỜ QUẢNG CÁO...", "WAITING FOR AD...");
        Add("QUẢNG CÁO THỬ", "TEST AD");
        Add("Chờ 5 giây để nhận thưởng x2", "Wait 5 seconds to claim x2");
        Add("Sau 5 giây có thể nhận x2", "Claim x2 after 5 seconds");
        Add("Thời gian còn lại: 5s", "Time remaining: 5s");
        Add("Đã xem xong. Bạn có thể nhận x2!", "Finished. You can claim x2!");
        Add("Quảng cáo chưa sẵn sàng. Bạn có thể nhận 100 hoặc thử lại.", "Ad unavailable. You can claim 100 or try again.");
        Add("Chưa nhận thưởng x2. Bạn có thể thử lại hoặc nhận 100.", "No x2 reward yet. You can retry or claim 100.");
        Add("CẤP ĐỘ", "LEVEL");
        Add("CẤP ĐỘ 1", "LEVEL 1");
        Add("HOÀN THÀNH CẤP ĐỘ 1", "COMPLETED LEVEL 1");
        Add("Bạn muốn về trang chủ?\nTim vô hạn đang hoạt động.", "Return home?\nUnlimited hearts are active.");
        Add("Bạn muốn chơi lại?\nTim vô hạn đang hoạt động.", "Retry this round?\nUnlimited hearts are active.");
        Add("Bạn muốn về trang chủ?\nVán đã kết thúc, không trừ thêm tim.", "Return home?\nThe round has ended. No extra heart will be spent.");
        Add("Bạn muốn chơi lại?\nVán đã kết thúc, không trừ thêm tim.", "Retry this round?\nThe round has ended. No extra heart will be spent.");

        // Special-level prompts and booster instructions.
        Add("SẴN SÀNG CHƯA?", "READY?");
        Add("ĐẠT ĐẾN CẤP ĐỘ 20", "REACH LEVEL 20");
        Add("MÀN CHƠI ĐANG KHÓA", "LEVEL LOCKED");
        Add("Mở ở cấp độ ", "Unlocks at level ");
        Add("Cấp hiện tại:", "Current level:");
        Add("Bạn đã sẵn sàng vào màn đặc biệt cấp\t           ?", "Ready to enter special level\t           ?");
        Add("Bạn đã sẵn sàng vào màn đặc biệt cấp", "Ready to enter special level");
        Add("Vượt qua các màn trước để mở khóa khu vực này.", "Complete the earlier levels to unlock this area.");
        Add("Kéo chiếc kéo vào súng đôi để tách thành hai súng đơn", "Drag the scissors onto a double gun to split it into two single guns");
        Add("Kéo chiếc kéo vào súng đôi để tách chúng ra thành súng đơn", "Drag the scissors onto a double gun to split it into single guns");
        Add("Kéo bàn tay vào súng muốn lấy để đưa súng lên khay", "Drag the hand onto a gun to move it to the tray");
    }

    public static string Translate(string value, bool english)
    {
        if (string.IsNullOrEmpty(value)) return value;
        string translated;
        return (english ? LocalizeCatalog.english : vietnamese).TryGetValue(value, out translated) ? translated : value;
    }

    private static void Add(string vi, string en)
    {
        english.Add(vi, en);
        vietnamese.Add(en, vi);
        // Serialized labels can retain Windows newlines; preserve their exact line endings.
        if (vi.IndexOf('\n') >= 0 && en.IndexOf('\n') >= 0)
        {
            english.Add(vi.Replace("\n", "\r\n"), en.Replace("\n", "\r\n"));
            vietnamese.Add(en.Replace("\n", "\r\n"), vi.Replace("\n", "\r\n"));
        }
    }
}
