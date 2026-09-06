# -*- coding: utf-8 -*-
"""慢性閉塞性肺疾患（COPD） 動画 全シーン音声 一括生成（CeVIO AI 夏色花梨）
「専門用語ゼロ」シリーズ。自動生成: generate_lecture_script.py → 心理設計版に手直し
音声=こども語（専門語は画面=字幕/インフォグラフィック側に置く二層構造）。
toneは cevio_tts.TONE_MAP のキー。声質パラメータは cevio_tts.py 側で一元管理。
出力先: public/tokutei_copd/*.wav

【この台本の心理設計メモ】
  一言目で気持ちよく : tsukami_01 で「動画を開いた」という行動を先に承認してから本題へ
  視聴者が大好き     : 「あなた」呼びで一対一。介護職の仕事そのものへの敬意を各章に置く
  行動を褒める       : 見た目や才能ではなく「開いた・見続けた・記録した・気づいた」を褒める
  進捗の承認         : 章の切れ目で「ここまで見たあなたは、もう〜」と積み上げを可視化
  例え話             : 誰でも知っている物だけ（スポンジ・ストロー・ろうそく・階段・福神漬け）
  マダンテを打たない : 在宅酸素は「これだけ」に絞り、残りは次回への引きにする
  CTAは相手の利益    : 登録の理由を「わたくしのため」ではなく「あなたの次の困りごと」に置く
"""
import sys, io, os, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

from cevio_tts import synthesize

OUT_DIR = "public/tokutei_copd"

LINES = [
    # ---------- つかみ ----------
    {"filename": "tokutei_copd_tsukami_01_intro_impact.wav", "text": "ごきげんよう、女神アテナですわ。まず、この動画を開いてくださったあなた。忙しい介護の合間に、利用者さんのために調べに来た。その時点で、もう大半の人より一歩先にいますわ。そんなあなたに、今日はこれをお届けします。COPDは「息が吸えない病気」ではなく、「息が吐けない病気」ですの。", "tone": "IGEN"},
    {"filename": "tokutei_copd_tsukami_02_merit.wav", "text": "このちがいが分かると、現場での見方がガラッと変わります。今日でCOPDの正体と、介護で命を救う「気づき」のコツが、まるごと分かりますわ。専門用語ゼロ、たとえ話だけでお届けしますの。", "tone": "GENKI"},
    {"filename": "tokutei_copd_tsukami_03_chirami.wav", "text": "そして最後に、在宅酸素を使っているかたのケアで、「これだけは絶対に守ってほしい、たったひとつのこと」をお教えします。知らないと命に関わることですから、最後まで見てくださいまし。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_tsukami_04_warmup.wav", "text": "……なんて、颯爽と登場したわたくしですが、「吐けない病気」と聞いて、うっかり「吐き出したいこと、わたくしにもあるわ」と遠い目をしてしまいましたの。女神も人の子ですわ。あなたも、たまには吐き出してくださいましね。", "tone": "SHONBORI"},

    # ---------- 目次 ----------
    {"filename": "tokutei_copd_toc_01_toc_intro.wav", "text": "今日は3つだけ、お話しますわ。3つなら、忙しいあなたでも持ち帰れますもの。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_toc_02_toc_one.wav", "text": "ひとつ目。COPDって、そもそも何？肺の中で何が起きているか、台所にあるもので、ズバッとたとえますわ。", "tone": "GENKI"},
    {"filename": "tokutei_copd_toc_03_toc_two.wav", "text": "ふたつ目。どんな症状が出て、どう悪化するか。特に「風邪が命取り」になる話、しっかり押さえますわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_toc_04_toc_three.wav", "text": "みっつ目。介護でできる、今日からの3つの実践。観る・吐く・つなぐ、ですわ。そして最後に、在宅酸素の「これだけ」ですの。", "tone": "NORMAL"},

    # ---------- 第一章 ----------
    {"filename": "tokutei_copd_honpen1_01_h1_intro.wav", "text": "第一章、COPDってなんですの？", "tone": "GENKI"},
    {"filename": "tokutei_copd_honpen1_02_h1_tatoeba.wav", "text": "たとえるなら、台所の「古いスポンジ」ですわ。新品のスポンジは、ギュッと握ったらパッと元に戻る。でもボロボロのスポンジは、握っても戻らないし、中の空気が出ていかない。COPDの肺は、まさにそれですの。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen1_03_h1_haike.wav", "text": "もう少しだけ、中を見てみますわね。肺の中には、ぶどうの房みたいな小さな袋が、左右合わせておよそ3億個あります。この袋で、酸素と血液がやりとりしている。ここまでは、ついてこられていますわね。さすがですわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen1_04_h1_tabako.wav", "text": "ここにタバコの煙を何十年も浴び続けると、ふたつのことが起きますわ。ひとつ、袋の壁が溶けてくっついて、大きくてスカスカの袋になる。これが「肺気腫」。ふたつ、空気の通り道が炎症で腫れて、痰がダラダラ出て細くなる。これが「慢性気管支炎」。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen1_05_h1_hakenai.wav", "text": "このふたつを合わせたのがCOPDですわ。大事なのは、「吸えない」んじゃなくて「吐けない」こと。たとえるなら、先を指でつまんだストローから息を吐こうとしている感じ。吐き切れなかった空気がどんどんたまって、胸がビア樽みたいに丸くなる。これが「樽状胸」ですわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen1_06_h1_genin.wav", "text": "原因のおよそ9割はタバコ。喫煙者のおよそ5人から6人に1人が発症するとされています。特に40歳以上の男性に多く、40歳以上の12人に1人に相当するとも言われますの。なのに病院に通っているのはそのほんの一部。「年のせい」と思って放置している人が、それはもう、山ほどいますわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen1_07_h1_modoranai.wav", "text": "そして最大の悲劇。一度壊れた肺は、元には戻りません。だから治療の目標は「治す」ではなく、「これ以上壊さない」。そして、それを毎日の現場で支えているのが、あなたですの。医者ではなく、あなたですわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen1_08_h1_summary.wav", "text": "まとめますわ。COPDは「タバコで肺がボロボロになる、吐けない病気」。肺気腫と慢性気管支炎のふたつが重なって、壊れた肺は戻らない。だから「進行を遅らせること」が全て。ここまで見たあなたは、もうCOPDの正体を説明できるレベルですわ。", "tone": "GENKI"},

    # ---------- 第二章 ----------
    {"filename": "tokutei_copd_honpen2_01_h2_intro.wav", "text": "第二章。症状と、風邪が命取りになる話ですわ。ここが、あなたの「気づく力」が一番活きる章ですの。", "tone": "GENKI"},
    {"filename": "tokutei_copd_honpen2_02_h2_sankyo.wav", "text": "COPDの三大症状を「さ・せ・た」で覚えてくださいまし。「さ」は息切れ、「せ」は咳、「た」は痰。この3つが、何年もかけてじわじわ忍び寄りますの。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen2_03_h2_susumu.wav", "text": "最初は「坂道でちょっとハアハアする」くらい。本人は「年のせいでしょ」と思っている。でもだんだん、着替えるだけで息切れ、座っているだけで苦しい、という状態になっていく。この「だんだん」を、毎日そばで見ているのは、あなただけですわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen2_04_h2_kuchi.wav", "text": "よく見ると、口を「ふー」とすぼめながら息を吐いているかたがいますわ。体が自分で編み出した技で、「口すぼめ呼吸」と言います。もし「あ、あの人だ」と顔が浮かんだなら、あなたはもう、しっかり観察できている証拠ですの。後の章で大事になりますから覚えておいてくださいまし。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen2_05_h2_yaseru.wav", "text": "それから意外な症状が「痩せる」。息をするだけで、健康なかたの何倍ものエネルギーを使うとされています。食べても追いつかないし、胃がふくらむと肺が圧迫されて苦しくなるから食べられない。だから「胸だけ樽みたいで体はガリガリ」というのが、COPDの典型的な体型ですわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen2_06_h2_zoaku.wav", "text": "そして最大の敵が「急性増悪」。健康な人の風邪は3日寝たら治る。でもCOPDのかたの風邪は「肺炎、入院、最悪そのまま」のコースに入りやすい。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen2_07_h2_kaidan.wav", "text": "恐ろしいのは、増悪するたびに肺の力が1段下がって、二度と上がってこないこと。階段を転げ落ちるように、ガクッガクッと悪くなる。だから「増悪させない」が、この病気の全てと言っても過言ではないですわ。", "tone": "IGEN"},
    {"filename": "tokutei_copd_honpen2_08_h2_signs.wav", "text": "介護のかたに見てほしいサインを「5つ」お伝えします。覚え方は「痰・息・熱・むくみ・ぼーっ」ですわ。痰の色が黄色や緑に変わる。いつもより息が荒い。熱っぽい。足がむくんでいる。そしてぼーっとしている、ウトウトしている。この5つですの。", "tone": "GENKI"},
    {"filename": "tokutei_copd_honpen2_09_h2_kinkyuu.wav", "text": "特に「ぼーっとしている、手がプルプル震えている」は超緊急。二酸化炭素が体にたまっているサインで、すぐ医師や看護師に報告してくださいまし。唇や爪が紫色になっていたら、救急のレベルですわ。そして、この5つを頭に入れた今のあなたは、昨日のあなたより確実に、ひとつ命を救える人になっていますの。", "tone": "MAJIME"},

    # ---------- 第三章 ----------
    {"filename": "tokutei_copd_honpen3_01_h3_intro.wav", "text": "第三章。介護で今日からできる3つの実践ですわ。名付けて「観る・吐く・つなぐ」ですの。", "tone": "GENKI"},
    {"filename": "tokutei_copd_honpen3_02_h3_one.wav", "text": "実践その一、「観る」。パルスオキシメーターの数字、毎日同じ時間に記録してほしいのですわ。もう記録しているあなた、それは立派な医療の一部ですの。大事なのは「その人の普段の数字」を知っておくこと。普段より3から4ほど下がったら、その日のうちに報告ですわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen3_03_h3_one_b.wav", "text": "測るときの注意。マニキュアをしていると正しく測れません。指が冷えていても数字がくるいます。体が動いている間も正確ではない。静かに、温かい手で、ですわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen3_04_h3_two.wav", "text": "実践その二、「吐く」。口すぼめ呼吸を一緒に練習してあげてほしいのですわ。誕生日のろうそくを「ふーっ」と細く長く吹き消すイメージ。吸うより吐くを長くするのがコツ。これだけで、肺にたまった空気が出やすくなり、息苦しさが和らぐことがありますわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen3_05_h3_two_b.wav", "text": "呼吸が苦しいときの楽な姿勢もお伝えします。前かがみになって、ひじをひざや台の上についた「前傾姿勢」が助けになりますわ。肩の力が抜けて、横隔膜が動きやすくなりますの。あなたが隣で一緒に「ふーっ」とやってあげるだけで、ご本人はずっと安心できますわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_honpen3_06_h3_three.wav", "text": "実践その三、「つなぐ」。感染を持ち込まないこと、これが最大の増悪予防ですわ。手洗い、口の中のケア、ワクチン。カレーの福神漬けみたいに、地味で、あって当たり前に見える。でも無いと一気に崩れる。この3つが、COPDのかたの命を守る盾になりますの。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_honpen3_07_h3_three_b.wav", "text": "そして約束の、在宅酸素の「これだけ」。酸素の流す量を、自分の判断で絶対に上げないでくださいまし。COPDのかたは、酸素を増やしすぎると、かえって呼吸が弱くなって、ぼーっとして危険になることがありますの。苦しそうでも、まず報告。そして火気は2メートル以上離す。この2つだけ、今日は持ち帰ってくださいまし。加湿や機械の扱いなど細かい話は、次回まるごと一本でお話しますわ。", "tone": "MAJIME"},

    # ---------- エンディング ----------
    {"filename": "tokutei_copd_ending_01_osarai_intro.wav", "text": "おさらいですわ。ここまで見続けたあなたへ、今日の3つ。", "tone": "GENKI"},
    {"filename": "tokutei_copd_ending_02_osarai_one.wav", "text": "ひとつ。COPDは「吐けない病気」。古いスポンジ、つまんだストロー。タバコで肺がボロボロになり、一度壊れたら元には戻らない。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_ending_03_osarai_two.wav", "text": "ふたつ。風邪が命取りになる急性増悪。「痰・息・熱・むくみ・ぼーっ」の5つのサインを見逃さない。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_ending_04_osarai_three.wav", "text": "みっつ。介護でできる「観る・吐く・つなぐ」。毎日の数字を記録して、口すぼめ呼吸を一緒に、そして感染を防いで異変はすぐ報告。酸素の量は自分で上げない、ですわ。", "tone": "GENKI"},
    {"filename": "tokutei_copd_ending_05_thanks.wav", "text": "今日も最後まで見てくださって、ありがとうございますわ。動画を開いた、最後まで見た、そして明日、現場で誰かの顔色を一秒長く見る。その一秒が、命を救いますの。あなたのその一秒を、女神はちゃんと見ておりますわ。", "tone": "MAJIME"},
    {"filename": "tokutei_copd_ending_06_cta.wav", "text": "次に現場で「これ、どうなんだろう」と困ったとき、すぐ戻ってこられるように、チャンネル登録しておいてくださいまし。高評価は、同じように困っている仲間に、この動画が届く手助けになりますの。ついでに、わたくしの羽根も、心なしか少しだけ、ふわっとしますわ。", "tone": "NORMAL"},
    {"filename": "tokutei_copd_ending_07_metajoke.wav", "text": "……ちなみに今日、「吐けない病気の話をする」と聞いて、スタッフに「アテナ様、ストレス発散できてますか」と心配されました。わたくし、女神ですわよ。肺は丈夫ですわ。たぶん。", "tone": "SHONBORI"},
    {"filename": "tokutei_copd_ending_08_close.wav", "text": "ごきげんよう、また次回ですわ。次回は、在宅酸素のまるごと一本。あなたを待っておりますわ。", "tone": "NORMAL"},
]


def main():
    print(f"=== 慢性閉塞性肺疾患（COPD） 音声生成(CeVIO夏色花梨) 開始 ({len(LINES)}本) ===")
    os.makedirs(OUT_DIR, exist_ok=True)
    start = time.time()
    for i, line in enumerate(LINES, 1):
        out_path = os.path.join(OUT_DIR, line["filename"])
        try:
            synthesize(line["text"], out_path, line["tone"])
            print(f"  [{i:02d}/{len(LINES)}] {line['filename']} ({os.path.getsize(out_path)//1024}KB)")
        except Exception as e:
            print(f"  [{i:02d}/{len(LINES)}] ERROR: {line['filename']} -- {e}")
        time.sleep(0.3)  # CeVIO詰まり対策の小休止
    print(f"=== 完了 ({time.time()-start:.1f}s) 出力先: {OUT_DIR}/ ===")


if __name__ == "__main__":
    main()
