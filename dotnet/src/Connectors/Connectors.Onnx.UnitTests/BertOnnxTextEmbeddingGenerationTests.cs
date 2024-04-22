﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Embeddings;
using xRetry;
using Xunit;

namespace SemanticKernel.Connectors.Onnx.UnitTests;

public class BertOnnxTextEmbeddingGenerationServiceTests
{
    private static readonly HttpClient s_client = new();

    [Fact]
    public void VerifyOptionsDefaults()
    {
        var options = new BertOnnxOptions();
        Assert.False(options.CaseSensitive);
        Assert.Equal(512, options.MaximumTokens);
        Assert.Equal("[CLS]", options.ClsToken);
        Assert.Equal("[UNK]", options.UnknownToken);
        Assert.Equal("[SEP]", options.SepToken);
        Assert.Equal("[PAD]", options.PadToken);
        Assert.Equal(NormalizationForm.FormD, options.UnicodeNormalization);
        Assert.Equal(EmbeddingPoolingMode.Mean, options.PoolingMode);
        Assert.False(options.NormalizeEmbeddings);
    }

    [Fact]
    public void RoundtripOptionsProperties()
    {
        var options = new BertOnnxOptions()
        {
            CaseSensitive = true,
            MaximumTokens = 128,
            ClsToken = "<A>",
            UnknownToken = "<B>",
            SepToken = "<C>",
            PadToken = "<D>",
            UnicodeNormalization = NormalizationForm.FormKC,
            PoolingMode = EmbeddingPoolingMode.MeanSquareRootTokensLength,
            NormalizeEmbeddings = true,
        };

        Assert.True(options.CaseSensitive);
        Assert.Equal(128, options.MaximumTokens);
        Assert.Equal("<A>", options.ClsToken);
        Assert.Equal("<B>", options.UnknownToken);
        Assert.Equal("<C>", options.SepToken);
        Assert.Equal("<D>", options.PadToken);
        Assert.Equal(NormalizationForm.FormKC, options.UnicodeNormalization);
        Assert.Equal(EmbeddingPoolingMode.MeanSquareRootTokensLength, options.PoolingMode);
        Assert.True(options.NormalizeEmbeddings);
    }

    [Fact]
    public void ValidateInvalidOptionsPropertiesThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BertOnnxOptions() { MaximumTokens = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new BertOnnxOptions() { MaximumTokens = -1 });

        Assert.Throws<ArgumentNullException>(() => new BertOnnxOptions() { ClsToken = null! });
        Assert.Throws<ArgumentException>(() => new BertOnnxOptions() { ClsToken = "   " });

        Assert.Throws<ArgumentNullException>(() => new BertOnnxOptions() { UnknownToken = null! });
        Assert.Throws<ArgumentException>(() => new BertOnnxOptions() { UnknownToken = "   " });

        Assert.Throws<ArgumentNullException>(() => new BertOnnxOptions() { SepToken = null! });
        Assert.Throws<ArgumentException>(() => new BertOnnxOptions() { SepToken = "   " });

        Assert.Throws<ArgumentNullException>(() => new BertOnnxOptions() { PadToken = null! });
        Assert.Throws<ArgumentException>(() => new BertOnnxOptions() { PadToken = "   " });

        Assert.Throws<ArgumentOutOfRangeException>(() => new BertOnnxOptions() { PoolingMode = (EmbeddingPoolingMode)4 });
    }

    [RetryFact(typeof(HttpRequestException))]
    public async Task ValidateEmbeddingsAreIdempotent()
    {
        Func<Task<BertOnnxTextEmbeddingGenerationService>>[] funcs =
        [
            GetBgeMicroV2ServiceAsync,
            GetAllMiniLML6V2Async,
        ];

        foreach (Func<Task<BertOnnxTextEmbeddingGenerationService>> func in funcs)
        {
            using BertOnnxTextEmbeddingGenerationService service = await func();

            string[] inputs =
            [
                "",
                " ",
                "A",
                "Hi",
                "This is a test. This is only a test.",
                "Toto, I’ve got a feeling we’re not in Kansas anymore.",
                string.Concat(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz ", 30)),
                "🙏➡️👤 for your ⏰",
            ];

            foreach (string input in inputs)
            {
                IList<ReadOnlyMemory<float>> results = await service.GenerateEmbeddingsAsync([input, input.ToUpperInvariant(), input.ToLowerInvariant()]);
                for (int i = 1; i < results.Count; i++)
                {
                    AssertEqualTolerance(results[0].Span, results[i].Span);
                }
            }
        }
    }

    [RetryFact(typeof(HttpRequestException))]
    public async Task ValidateExpectedEmbeddingsForBgeMicroV2()
    {
        string modelPath = await GetTestFilePath(BgeMicroV2ModelUrl);
        string vocabPath = await GetTestFilePath(BgeMicroV2VocabUrl);

        using Stream modelStream = File.OpenRead(modelPath);
        using Stream vocabStream = File.OpenRead(vocabPath);

        // Test with all the different ways a service can be created
        foreach (BertOnnxOptions? options in new[] { new BertOnnxOptions(), null })
        {
            using var service1 = BertOnnxTextEmbeddingGenerationService.Create(modelPath, vocabPath, options);
            using var service2 = BertOnnxTextEmbeddingGenerationService.Create(modelStream, vocabStream, options);
            modelStream.Position = vocabStream.Position = 0;

            using var service3 = await BertOnnxTextEmbeddingGenerationService.CreateAsync(modelPath, vocabPath, options);
            using var service4 = await BertOnnxTextEmbeddingGenerationService.CreateAsync(modelStream, vocabStream, options);
            modelStream.Position = vocabStream.Position = 0;

            using var service5 = (BertOnnxTextEmbeddingGenerationService)Kernel.CreateBuilder().AddBertOnnxTextEmbeddingGeneration(modelPath, vocabPath, options).Build().GetRequiredService<ITextEmbeddingGenerationService>();
            using var service6 = (BertOnnxTextEmbeddingGenerationService)Kernel.CreateBuilder().AddBertOnnxTextEmbeddingGeneration(modelStream, vocabStream, options).Build().GetRequiredService<ITextEmbeddingGenerationService>();
            modelStream.Position = vocabStream.Position = 0;

            var b = Kernel.CreateBuilder();
            b.Services.AddBertOnnxTextEmbeddingGeneration(modelPath, vocabPath, options);
            using var service7 = (BertOnnxTextEmbeddingGenerationService)b.Build().GetRequiredService<ITextEmbeddingGenerationService>();
            b.Services.Clear();
            b.Services.AddBertOnnxTextEmbeddingGeneration(modelStream, vocabStream, options);
            using var service8 = (BertOnnxTextEmbeddingGenerationService)b.Build().GetRequiredService<ITextEmbeddingGenerationService>();
            modelStream.Position = vocabStream.Position = 0;

            foreach (var service in new[] { service1, service2, service3, service4, service5, service6, service7, service8 })
            {
                Assert.Empty(service.Attributes);

                // Inputs generated by running this Python code:
                //     from sentence_transformers import SentenceTransformer
                //     sentences = ["This is an example sentence", "Each sentence is converted"]
                //     model = SentenceTransformer('TaylorAI/bge-micro-v2')
                //     embeddings = model.encode(sentences)
                //     print(*embeddings[0], sep=", ")
                //     print(*embeddings[1], sep=", ")
                (string Input, float[] Embedding)[] samples =
                [
                    ("This is an example sentence", [-0.5157151f, -0.18483242f, -0.024855154f, -0.13922776f, -0.072655626f, -0.14032415f, 0.6466194f, 0.28644928f, 0.23654939f, -0.184456f, 0.052697394f, -0.27464885f, -0.15709765f, 0.07284545f, 0.1649531f, 0.19802274f, -0.16668232f, 0.106417134f, -0.5961622f, 0.120383136f, 0.9766301f, 0.18895401f, -0.30458942f, -0.07573986f, 0.35496518f, 0.34536785f, 0.21772523f, -0.15485178f, 0.25956184f, -0.5971247f, -0.26436645f, 0.049176477f, 0.17538252f, 0.053731553f, 0.18673553f, 0.21818502f, -0.53409797f, 0.1597614f, -0.5581393f, 0.3304148f, 0.08020442f, 0.3004675f, -0.17133074f, 0.16965258f, -0.1687865f, -0.20889947f, -0.17347299f, -0.18619454f, -0.0031209993f, -0.115003005f, -0.1340431f, -0.065183856f, -0.15632676f, -0.283858f, 0.3012186f, 0.20706663f, 0.46964383f, 0.33754826f, 0.13068083f, -0.113442235f, 0.48451662f, 0.04757864f, -1.0177306f, 0.26682487f, 0.35435796f, 0.18991317f, -0.09538897f, 0.019450301f, 0.047304023f, 0.33794662f, 0.04346403f, -0.082397714f, 0.12557605f, 0.7214249f, -0.2972784f, -0.032897063f, -0.014510592f, -0.13479017f, -0.11902117f, -0.124368034f, -0.08499669f, -0.02626245f, 0.17537363f, -0.18673882f, -0.45975524f, -0.21523671f, 0.09817474f, -0.21201028f, 0.2668921f, 0.030238701f, -0.2875212f, -0.29757038f, -0.044557817f, 0.15278347f, -0.2302485f, -0.15557694f, 0.19477595f, 0.018366996f, 0.14310992f, 1.0340254f, -0.14803658f, 0.10275917f, 0.24706373f, -0.29378265f, 0.2243055f, -0.1429121f, 0.1727231f, -0.27787137f, -0.27035895f, -0.030546295f, -0.44832778f, 0.24289069f, 0.29438433f, -0.26721075f, 0.14328241f, -0.40703794f, 0.42846856f, -0.10638199f, -0.020640552f, -0.16759089f, 0.009304181f, -0.04581476f, -0.060340293f, 0.059741654f, 0.138177f, -0.3175531f, 0.48137474f, 0.34072623f, 0.31291014f, -0.1918683f, 0.39636797f, -0.53026897f, -0.3341995f, 0.23552401f, -0.14521062f, -0.12095903f, 0.29756752f, 0.07932409f, 0.08463049f, -0.44085723f, 0.015109009f, -0.575077f, -0.35287866f, -0.4731309f, -0.41332778f, 0.56492776f, 0.14517987f, 0.07356074f, -0.39172816f, -0.0059272987f, -0.10639355f, 0.031566177f, 0.13750012f, -0.22036016f, 0.010432887f, 0.4472182f, 0.6101073f, 0.00074800424f, -0.057303447f, 0.27033067f, 0.07550515f, -0.22163253f, -0.3159139f, 0.44562748f, 0.26698872f, -0.6491033f, -0.00534522f, -0.06964374f, -0.007006743f, -0.2884609f, 0.1498746f, 0.075905375f, -0.62091637f, 0.31652737f, 0.3103272f, 0.3122592f, -0.2806999f, -0.15576728f, -0.18513246f, 0.0871565f, 0.27063182f, -0.25300217f, -0.54549205f, 0.29495722f, 0.115334176f, -0.3249089f, 0.05564102f, -0.37034506f, 0.09348737f, 0.13965131f, -0.3942195f, 0.4092014f, -0.1559632f, -0.20598184f, -0.6145921f, 0.06501871f, 0.21684805f, -0.58250314f, 0.13055332f, -0.37380242f, 0.10620829f, 0.31163308f, -0.028585939f, -0.109412216f, -0.027620826f, 0.06073291f, 0.13825443f, -0.011065506f, -0.13500609f, 0.07023274f, -0.54256576f, 0.03908627f, -0.22387981f, 0.37132427f, -0.15852274f, -0.36472347f, -0.20229885f, 0.49056253f, 0.22915308f, 0.08973607f, -0.39936402f, -0.4133983f, 0.19044447f, -1.5060136f, 0.10460026f, 0.38686958f, -0.38257426f, 0.09412465f, 0.06998003f, 0.15060483f, -0.024935398f, -0.14254098f, -0.050634492f, 0.47114816f, -0.49116158f, 0.44650203f, -0.34633717f, 0.112378515f, 0.06398543f, -0.2578128f, -0.16385294f, 0.21114261f, 0.1176803f, 0.26751f, -0.10888121f, 0.27298358f, -0.7515298f, 0.057275366f, -0.15472014f, 1.1640681f, 0.74034554f, 0.46668515f, -0.27005175f, 0.14234237f, -0.13888265f, -0.04149701f, -0.4620673f, -0.06777647f, -0.14131258f, -0.06292421f, -0.11160091f, -0.37824768f, 0.1363496f, -0.053488694f, 0.35645443f, -0.2850037f, 0.03682816f, -0.013400972f, -0.04572044f, -0.34677473f, -0.12916856f, -0.26508957f, 0.63653994f, 0.2510722f, -0.065791376f, 0.18835366f, -0.015346631f, 0.29692408f, -0.083626665f, -0.46156904f, -0.116871215f, -0.022547228f, 0.12905477f, -0.041697938f, 0.14600737f, 0.18852365f, -0.2929062f, 0.20489062f, 0.37139255f, 0.15763652f, -0.45193034f, -0.2340064f, 0.13947651f, -0.19313012f, 0.6072279f, 0.17079315f, -0.60778147f, 0.025057724f, 0.23958695f, 0.09187108f, -0.020909315f, -0.21719012f, -0.21682595f, 0.122083746f, -0.17339528f, 0.036168676f, 0.05860231f, 0.3373259f, 0.23916484f, 0.2149777f, 0.10672321f, 0.5943106f, -0.16928284f, -0.13003561f, -0.04250761f, -0.2476354f, 0.07271506f, 0.13103546f, -0.29819822f, -1.6984111f, 0.31073052f, 0.40687817f, 0.21613891f, -0.025025155f, 0.46117622f, -0.0874816f, -0.11365145f, -0.79055214f, 0.20257166f, -0.2764636f, -0.0704192f, 0.123011805f, -0.032466434f, -0.16304152f, 0.03409268f, 0.37523815f, 0.08962136f, 0.31773967f, -0.31791234f, 0.15886307f, 0.14318463f, 1.0989486f, -0.40212637f, 0.5041059f, 0.10564138f, -0.14110602f, -0.12608881f, 0.61138386f, 0.10941125f, 0.03273521f, -0.193009f, 0.8789654f, -0.12541887f, 0.1322615f, -0.16763277f, 0.20899202f, 0.21551795f, 0.45041195f, 0.052844554f, -0.43125144f, 0.35993344f, -0.44850373f, 0.36767358f, 0.5982758f, 0.20872377f, 0.37044856f, -0.54784334f, -0.4885538f, 0.15849254f, 0.061219603f, 0.02141064f, 0.020939479f, 0.31681973f, 0.34712973f, 0.23357531f, -0.10348662f, -0.28897852f, 0.013509659f, 0.010176753f, -0.108670406f, -0.10791451f, 0.663982f, 0.2210705f, 0.06329439f]),
                    ("Each sentence is converted", [-0.20611618f, -0.002688757f, -0.111204125f, 0.1147305f, -0.17492668f, -0.0971449f, 0.4068564f, 0.15559201f, 0.26603976f, 0.16648461f, -0.19747871f, -0.27353737f, 0.21562691f, -0.113559745f, 0.108241834f, 0.07105198f, -0.27027193f, 0.04995221f, -0.5075852f, -0.1617351f, 0.3702642f, -0.10660389f, 0.02980175f, -0.2970495f, 0.3164048f, 0.57045454f, 0.1505325f, -0.1531308f, -0.036590848f, -0.7927463f, -0.1500182f, -0.09659263f, 0.1808242f, -0.0003509596f, 0.1792987f, 0.2235533f, -0.4362891f, 0.14326544f, -0.22085004f, 0.35425743f, -0.012296041f, 0.33671084f, 0.08147127f, -0.15094213f, -0.060471784f, -0.38949648f, -0.32394364f, 0.22198884f, 0.15842995f, 0.10660344f, -0.24982567f, -0.2885716f, -0.28190053f, -0.04913057f, 0.37472722f, 0.3077549f, 0.044403862f, 0.45348445f, 0.22628604f, -0.085618734f, 0.20035471f, 0.5076632f, -1.113316f, 0.19863419f, -0.0012943111f, -0.03569807f, 0.087357976f, -0.0053361207f, -0.05033088f, 0.38103834f, -0.16297866f, -0.24583201f, -0.0523369f, 0.46682492f, 0.16835456f, 0.00223771f, -0.24686284f, -0.13878813f, -0.11443451f, 0.042145133f, 0.2101243f, -0.49921736f, 0.035280082f, -0.052376848f, -0.14526382f, -0.19259648f, 0.14355347f, 0.07098616f, 0.05347444f, 0.15262802f, -0.3127053f, -0.31114718f, 0.07842686f, 0.034230642f, -0.2000854f, -0.23419535f, -0.04681025f, 0.09900249f, 0.43006715f, 1.2887012f, -0.05088989f, 0.17736197f, 0.5022547f, -0.3868835f, -0.08662698f, -0.10146138f, 0.093568325f, -0.113100626f, -0.1886593f, 0.042257786f, -0.6125443f, -0.26039907f, 0.24071597f, -0.27879748f, 0.09503179f, 0.20986517f, 0.064997114f, 0.17523013f, 0.0944059f, 0.13191073f, 0.11074757f, 0.21201818f, -0.53156525f, 0.042199835f, 0.021026244f, -0.16116671f, 0.42700586f, 0.37678054f, 0.36959124f, 0.044647932f, 0.31546673f, 0.25417826f, -0.47580716f, -0.024513176f, -0.07024818f, -0.14139508f, 0.22642708f, 0.021366304f, 0.16724725f, -0.22943532f, 0.038373794f, -0.29075345f, -0.04706791f, -0.0013847897f, -0.1779707f, 0.9908135f, -0.07467189f, -0.28277895f, -0.31488314f, 0.30481723f, -0.15915792f, 0.29893667f, 0.33740866f, -0.5880918f, -0.17124778f, 0.061184417f, 0.27691087f, -0.5461984f, -0.32614335f, 0.10077208f, 0.2787413f, 0.08547622f, -0.15954112f, 0.5842795f, 0.41823733f, -0.30494013f, 0.04445922f, 0.13764273f, -0.06897315f, -0.32131013f, 0.19616558f, 0.043547317f, -0.6933572f, 0.18542205f, 0.37595809f, 0.013603198f, -0.0866761f, -0.30194864f, -0.11063865f, -0.004179746f, 0.21519697f, -0.10848287f, -0.3569528f, 0.34449396f, 0.104068734f, 0.010376841f, -0.20464492f, -0.2009803f, 0.09205555f, 0.21292095f, -0.02343633f, 0.33992347f, -0.16497074f, -0.11151347f, -0.14962883f, -0.16688241f, 0.08150462f, -0.07582331f, 0.02321508f, -0.19145453f, 0.30194813f, 0.1619022f, -0.47716478f, -0.41828284f, 0.16753085f, -0.2810092f, -0.02217365f, 0.10595674f, -0.12097738f, 0.6465837f, -0.14917056f, -0.08032517f, 0.08433825f, 0.21088593f, -0.17868309f, -0.3775384f, -0.1045889f, 0.3917651f, 0.20975995f, 0.042033505f, -0.32310867f, -0.3521098f, 0.05636993f, -1.3475052f, 0.08304601f, 0.52438647f, -0.069034256f, 0.28510022f, 0.1165623f, -0.1458966f, -0.16453443f, 0.030458137f, 0.12665416f, 0.43200096f, -0.3170686f, 0.09890106f, -0.13503574f, -0.08410556f, 0.008680835f, -0.061507285f, 0.2171539f, 0.053703025f, 0.0047395476f, 0.21582556f, -0.048322767f, 0.41337624f, -0.9263349f, -0.08182155f, -0.10235953f, 1.0671576f, 0.59560245f, 0.47950968f, 0.020047234f, 0.35482824f, -0.16750951f, 0.17371273f, -0.37975633f, 0.4764653f, 0.030113121f, 0.1048407f, 0.07464028f, -0.016163299f, 0.039777312f, 0.41568685f, 0.31103256f, -0.2905521f, -0.32959083f, -0.276707f, -0.08244118f, -0.19626872f, -0.25713217f, -0.07012958f, 0.29580548f, 0.22220325f, -0.12865375f, 0.29315406f, -0.034061354f, 0.04724068f, -0.13187037f, -0.3728216f, 0.037293665f, 0.016591653f, -0.33842075f, -0.105650455f, 0.3135222f, -0.12911738f, -0.080178745f, 0.007035022f, 0.081988566f, 0.25299695f, -0.16541593f, -0.031563442f, -0.0003826196f, -0.06408165f, 0.039635688f, -0.1439694f, -0.26424268f, -0.15437256f, 0.32760164f, -0.39593825f, 0.09374673f, -0.15134661f, -0.15289468f, 0.42596254f, -0.34903678f, 0.10410272f, -0.010330292f, 0.3854884f, 0.1673473f, 0.14944296f, 0.3919189f, -0.050781537f, -0.0033439647f, 0.13987668f, -0.02843976f, -0.1312383f, 0.19214489f, 0.09281311f, -0.17178994f, -1.4415573f, -0.08487939f, -0.07362995f, -0.06951893f, 0.0963266f, 0.13399442f, 0.19361098f, 0.16463749f, -0.46581915f, 0.3292155f, -0.047704715f, 0.23742552f, -0.022593116f, -0.2545283f, 0.19410999f, 0.033487078f, 0.38724947f, 0.18239449f, 0.12916456f, -0.4910551f, 0.12860589f, 0.27904502f, 1.101342f, -0.18340228f, -0.04881097f, 0.14408469f, 0.028418904f, -0.11697259f, 0.47042826f, 0.18886185f, 0.0679057f, -0.29135367f, 0.57991606f, 0.042119365f, 0.0025073104f, 0.0677574f, -0.18624912f, 0.1542291f, 0.27249455f, 0.19006579f, -0.56617993f, 0.13161667f, -0.09931987f, -0.23538037f, 0.7121482f, -0.06824718f, -0.0013868908f, -0.6173385f, -0.53164536f, -0.11273178f, -0.19154763f, 0.103781946f, -0.120197795f, -0.36043325f, 0.07437929f, 0.3102483f, -0.1449395f, -0.32500622f, 0.20257138f, -0.0063248686f, -0.22025955f, -0.2684462f, 0.14406686f, 0.2146815f, -0.3316005f])
                ];

                foreach (var (Input, Embedding) in samples)
                {
                    IList<ReadOnlyMemory<float>> results = await service.GenerateEmbeddingsAsync([Input]);
                    AssertEqualTolerance(Embedding, results[0].Span);
                }
            }
        }
    }

    [RetryFact(typeof(HttpRequestException))]
    public async Task ValidateExpectedEmbeddingsForAllMiniLML6V2()
    {
        using BertOnnxTextEmbeddingGenerationService service = await GetAllMiniLML6V2Async();

        // Inputs generated by running this Python code:
        //     from sentence_transformers import SentenceTransformer
        //     sentences = ["This is an example sentence", "Each sentence is converted"]
        //     model = SentenceTransformer('sentence-transformers/all-MiniLM-L6-v2')
        //     embeddings = model.encode(sentences)
        //     print(*embeddings[0], sep=", ")
        //     print(*embeddings[1], sep=", ")
        (string Input, float[] Embedding)[] samples =
        [
            ("This is an example sentence", [6.76569119e-02f, 6.34959862e-02f, 4.87131625e-02f, 7.93049634e-02f, 3.74480709e-02f, 2.65275245e-03f, 3.93749885e-02f, -7.09843030e-03f, 5.93614168e-02f, 3.15370075e-02f, 6.00980520e-02f, -5.29052801e-02f, 4.06067595e-02f, -2.59308498e-02f, 2.98428256e-02f, 1.12689065e-03f, 7.35148787e-02f, -5.03818244e-02f, -1.22386575e-01f, 2.37028543e-02f, 2.97265109e-02f, 4.24768552e-02f, 2.56337635e-02f, 1.99514860e-03f, -5.69190569e-02f, -2.71598138e-02f, -3.29035595e-02f, 6.60249069e-02f, 1.19007170e-01f, -4.58791293e-02f, -7.26214573e-02f, -3.25840563e-02f, 5.23413755e-02f, 4.50553223e-02f, 8.25305190e-03f, 3.67024280e-02f, -1.39415143e-02f, 6.53918609e-02f, -2.64272187e-02f, 2.06402605e-04f, -1.36643695e-02f, -3.62810344e-02f, -1.95043758e-02f, -2.89738402e-02f, 3.94270197e-02f, -8.84090811e-02f, 2.62421113e-03f, 1.36713935e-02f, 4.83062640e-02f, -3.11566275e-02f, -1.17329195e-01f, -5.11690453e-02f, -8.85288045e-02f, -2.18962915e-02f, 1.42986495e-02f, 4.44167964e-02f, -1.34815173e-02f, 7.43392780e-02f, 2.66382825e-02f, -1.98762808e-02f, 1.79191604e-02f, -1.06051974e-02f, -9.04263109e-02f, 2.13268995e-02f, 1.41204834e-01f, -6.47178525e-03f, -1.40383001e-03f, -1.53609701e-02f, -8.73572156e-02f, 7.22173899e-02f, 2.01403126e-02f, 4.25587781e-02f, -3.49013619e-02f, 3.19490908e-04f, -8.02971721e-02f, -3.27472277e-02f, 2.85268407e-02f, -5.13657928e-02f, 1.09389201e-01f, 8.19327980e-02f, -9.84040126e-02f, -9.34096277e-02f, -1.51292188e-02f, 4.51248959e-02f, 4.94172387e-02f, -2.51867827e-02f, 1.57077387e-02f, -1.29290730e-01f, 5.31893782e-03f, 4.02343180e-03f, -2.34571360e-02f, -6.72982708e-02f, 2.92279720e-02f, -2.60845404e-02f, 1.30624948e-02f, -3.11663151e-02f, -4.82713953e-02f, -5.58859184e-02f, -3.87504958e-02f, 1.20010905e-01f, -1.03924125e-02f, 4.89704832e-02f, 5.53536899e-02f, 4.49357927e-02f, -4.00980143e-03f, -1.02959752e-01f, -2.92968526e-02f, -5.83402663e-02f, 2.70473082e-02f, -2.20169257e-02f, -7.22241402e-02f, -4.13869843e-02f, -1.93298087e-02f, 2.73329811e-03f, 2.77024054e-04f, -9.67588946e-02f, -1.00574657e-01f, -1.41923223e-02f, -8.07891712e-02f, 4.53925095e-02f, 2.45041065e-02f, 5.97613640e-02f, -7.38184974e-02f, 1.19844358e-02f, -6.63403794e-02f, -7.69045427e-02f, 3.85158025e-02f, -5.59362366e-33f, 2.80013755e-02f, -5.60785271e-02f, -4.86601666e-02f, 2.15569437e-02f, 6.01981059e-02f, -4.81402315e-02f, -3.50247324e-02f, 1.93314143e-02f, -1.75151899e-02f, -3.89210507e-02f, -3.81067395e-03f, -1.70287658e-02f, 2.82099284e-02f, 1.28290970e-02f, 4.71600592e-02f, 6.21030554e-02f, -6.43588975e-02f, 1.29285574e-01f, -1.31231090e-02f, 5.23069799e-02f, -3.73680927e-02f, 2.89094709e-02f, -1.68980937e-02f, -2.37330273e-02f, -3.33491713e-02f, -5.16762212e-02f, 1.55357225e-02f, 2.08802726e-02f, -1.25372009e-02f, 4.59578782e-02f, 3.72720025e-02f, 2.80566625e-02f, -5.90005033e-02f, -1.16988355e-02f, 4.92182411e-02f, 4.70328629e-02f, 7.35487789e-02f, -3.70530188e-02f, 3.98458820e-03f, 1.06412349e-02f, -1.61528107e-04f, -5.27165905e-02f, 2.75927819e-02f, -3.92921343e-02f, 8.44717622e-02f, 4.86860387e-02f, -4.85872617e-03f, 1.79948937e-02f, -4.28568944e-02f, 1.23375356e-02f, 6.39952952e-03f, 4.04823199e-02f, 1.48886638e-02f, -1.53941503e-02f, 7.62948319e-02f, 2.37043910e-02f, 4.45236862e-02f, 5.08196019e-02f, -2.31252168e-03f, -1.88737269e-02f, -1.23335645e-02f, 4.66001406e-02f, -5.63438199e-02f, 6.29927143e-02f, -3.15535367e-02f, 3.24911959e-02f, 2.34673023e-02f, -6.55438974e-02f, 2.01709140e-02f, 2.57082339e-02f, -1.23869041e-02f, -8.36491678e-03f, -6.64377883e-02f, 9.43073556e-02f, -3.57093066e-02f, -3.42483260e-02f, -6.66355295e-03f, -8.01526755e-03f, -3.09711322e-02f, 4.33012545e-02f, -8.21402203e-03f, -1.50795028e-01f, 3.07691768e-02f, 4.00719084e-02f, -3.79293561e-02f, 1.93212717e-03f, 4.00530547e-02f, -8.77075419e-02f, -3.68490554e-02f, 8.57962202e-03f, -3.19251716e-02f, -1.25257727e-02f, 7.35540017e-02f, 1.34736649e-03f, 2.05918178e-02f, 2.71098238e-33f, -5.18576838e-02f, 5.78361228e-02f, -9.18985456e-02f, 3.94421853e-02f, 1.05576515e-01f, -1.96911674e-02f, 6.18402325e-02f, -7.63465241e-02f, 2.40880344e-02f, 9.40048993e-02f, -1.16535433e-01f, 3.71198766e-02f, 5.22425398e-02f, -3.95856798e-03f, 5.72214201e-02f, 5.32849785e-03f, 1.24016888e-01f, 1.39022414e-02f, -1.10249659e-02f, 3.56053263e-02f, -3.30754593e-02f, 8.16574395e-02f, -1.52003448e-02f, 6.05585575e-02f, -6.01397939e-02f, 3.26102450e-02f, -3.48296240e-02f, -1.69881694e-02f, -9.74907354e-02f, -2.71484070e-02f, 1.74709782e-03f, -7.68982321e-02f, -4.31858189e-02f, -1.89984571e-02f, -2.91660987e-02f, 5.77488355e-02f, 2.41821967e-02f, -1.16902078e-02f, -6.21434860e-02f, 2.84351315e-02f, -2.37535409e-04f, -2.51783151e-02f, 4.39640554e-03f, 8.12840089e-02f, 3.64184454e-02f, -6.04006499e-02f, -3.65517475e-02f, -7.93748796e-02f, -5.08522429e-03f, 6.69698417e-02f, -1.17784373e-01f, 3.23743410e-02f, -4.71252352e-02f, -1.34459678e-02f, -9.48444828e-02f, 8.24951194e-03f, -1.06749050e-02f, -6.81881458e-02f, 1.11814507e-03f, 2.48020347e-02f, -6.35889545e-02f, 2.84493268e-02f, -2.61303764e-02f, 8.58111307e-02f, 1.14682287e-01f, -5.35345376e-02f, -5.63588776e-02f, 4.26009260e-02f, 1.09454552e-02f, 2.09578965e-02f, 1.00131147e-01f, 3.26051265e-02f, -1.84208766e-01f, -3.93209048e-02f, -6.91454858e-02f, -6.38104379e-02f, -6.56386092e-02f, -6.41250517e-03f, -4.79612611e-02f, -7.68133178e-02f, 2.95384377e-02f, -2.29948387e-02f, 4.17037010e-02f, -2.50047818e-02f, -4.54510376e-03f, -4.17136475e-02f, -1.32289520e-02f, -6.38357699e-02f, -2.46474030e-03f, -1.37337688e-02f, 1.68976635e-02f, -6.30398169e-02f, 8.98880437e-02f, 4.18170951e-02f, -1.85687356e-02f, -1.80442186e-08f, -1.67997926e-02f, -3.21578048e-02f, 6.30383715e-02f, -4.13092151e-02f, 4.44819145e-02f, 2.02464475e-03f, 6.29592612e-02f, -5.17367665e-03f, -1.00444453e-02f, -3.05640027e-02f, 3.52673046e-02f, 5.58581725e-02f, -4.67124805e-02f, 3.45103107e-02f, 3.29578072e-02f, 4.30114679e-02f, 2.94360649e-02f, -3.03164832e-02f, -1.71107780e-02f, 7.37484246e-02f, -5.47909848e-02f, 2.77515016e-02f, 6.20168634e-03f, 1.58800632e-02f, 3.42978686e-02f, -5.15748607e-03f, 2.35079788e-02f, 7.53135979e-02f, 1.92843266e-02f, 3.36197168e-02f, 5.09103686e-02f, 1.52497083e-01f, 1.64207816e-02f, 2.70528663e-02f, 3.75162140e-02f, 2.18552891e-02f, 5.66333942e-02f, -3.95746306e-02f, 7.12313578e-02f, -5.41377142e-02f, 1.03762979e-03f, 2.11852882e-02f, -3.56309302e-02f, 1.09016903e-01f, 2.76532234e-03f, 3.13997120e-02f, 1.38418446e-03f, -3.45738865e-02f, -4.59277928e-02f, 2.88083628e-02f, 7.16903526e-03f, 4.84684780e-02f, 2.61018146e-02f, -9.44074709e-03f, 2.82169525e-02f, 3.48724164e-02f, 3.69099118e-02f, -8.58950801e-03f, -3.53205763e-02f, -2.47856900e-02f, -1.91920940e-02f, 3.80708203e-02f, 5.99653088e-02f, -4.22287323e-02f]),
            ("Each sentence is converted", [8.64386037e-02f, 1.02762647e-01f, 5.39456727e-03f, 2.04443280e-03f, -9.96339694e-03f, 2.53855158e-02f, 4.92875241e-02f, -3.06265764e-02f, 6.87255040e-02f, 1.01365931e-02f, 7.75397941e-02f, -9.00807232e-02f, 6.10621506e-03f, -5.69898486e-02f, 1.41714485e-02f, 2.80491598e-02f, -8.68465081e-02f, 7.64399171e-02f, -1.03491329e-01f, -6.77438080e-02f, 6.99946657e-02f, 8.44250694e-02f, -7.24910991e-03f, 1.04770474e-02f, 1.34020830e-02f, 6.77577108e-02f, -9.42086354e-02f, -3.71690169e-02f, 5.22617251e-02f, -3.10853291e-02f, -9.63407159e-02f, 1.57716852e-02f, 2.57866886e-02f, 7.85245448e-02f, 7.89948776e-02f, 1.91516057e-02f, 1.64356343e-02f, 3.10086878e-03f, 3.81311439e-02f, 2.37090699e-02f, 1.05389562e-02f, -4.40645143e-02f, 4.41738665e-02f, -2.58728098e-02f, 6.15378618e-02f, -4.05427665e-02f, -8.64139944e-02f, 3.19722705e-02f, -8.90667376e-04f, -2.44437382e-02f, -9.19721350e-02f, 2.33939514e-02f, -8.30293223e-02f, 4.41510566e-02f, -2.49693245e-02f, 6.23020120e-02f, -1.30354415e-03f, 7.51395673e-02f, 2.46384963e-02f, -6.47244453e-02f, -1.17727734e-01f, 3.83392312e-02f, -9.11767483e-02f, 6.35446012e-02f, 7.62739703e-02f, -8.80241171e-02f, 9.54560284e-03f, -4.69717793e-02f, -8.41740668e-02f, 3.88823822e-02f, -1.14393510e-01f, 6.28854241e-03f, -3.49361897e-02f, 2.39750277e-02f, -3.31316963e-02f, -1.57243740e-02f, -3.78955565e-02f, -8.81249737e-03f, 7.06119090e-02f, 3.28066461e-02f, 2.03669094e-03f, -1.12279013e-01f, 6.79722289e-03f, 1.22765722e-02f, 3.35303470e-02f, -1.36201037e-02f, -2.25489810e-02f, -2.25228742e-02f, -2.03195214e-02f, 5.04297316e-02f, -7.48652667e-02f, -8.22822526e-02f, 7.65962377e-02f, 4.93392199e-02f, -3.75553556e-02f, 1.44634647e-02f, -5.72457761e-02f, -1.79954153e-02f, 1.09697960e-01f, 1.19462803e-01f, 8.09222518e-04f, 6.17057718e-02f, 3.26321982e-02f, -1.30780116e-01f, -1.48636609e-01f, -6.16232567e-02f, 4.33886163e-02f, 2.67129298e-02f, 1.39786340e-02f, -3.94002609e-02f, -2.52711680e-02f, 3.87739856e-03f, 3.58664617e-02f, -6.15420155e-02f, 3.76660600e-02f, 2.67565399e-02f, -3.82659324e-02f, -3.54793258e-02f, -2.39227880e-02f, 8.67977440e-02f, -1.84063073e-02f, 7.71039426e-02f, 1.39864522e-03f, 7.00383112e-02f, -4.77877557e-02f, -7.89819658e-02f, 5.10814264e-02f, -2.99868223e-33f, -3.91646028e-02f, -2.56210356e-03f, 1.65210236e-02f, 9.48940869e-03f, -5.66219315e-02f, 6.57783076e-02f, -4.77002710e-02f, 1.11662066e-02f, -5.73558100e-02f, -9.16262530e-03f, -2.17521060e-02f, -5.59531599e-02f, -1.11423032e-02f, 9.32793170e-02f, 1.66765396e-02f, -1.36723407e-02f, 4.34388258e-02f, 1.87238981e-03f, 7.29950890e-03f, 5.16332127e-02f, 4.80608642e-02f, 1.35341406e-01f, -1.71738844e-02f, -1.29698543e-02f, -7.50109702e-02f, 2.61107795e-02f, 2.69801971e-02f, 7.83074822e-04f, -4.87270430e-02f, 1.17842732e-02f, -4.59580645e-02f, -4.83213551e-02f, -1.95670929e-02f, 1.93889327e-02f, 1.98806971e-02f, 1.67432167e-02f, 9.87801328e-02f, -2.74087712e-02f, 2.34809052e-02f, 3.70226824e-03f, -6.14514835e-02f, -1.21230958e-03f, -9.50474385e-03f, 9.25151072e-03f, 2.38443799e-02f, 8.61232057e-02f, 2.26789843e-02f, 5.45111892e-04f, 3.47128771e-02f, 6.25467254e-03f, -6.92775892e-03f, 3.92400399e-02f, 1.15674892e-02f, 3.26280147e-02f, 6.22155443e-02f, 2.76114717e-02f, 1.86883733e-02f, 3.55805866e-02f, 4.11796086e-02f, 1.54782236e-02f, 4.22691591e-02f, 3.82248238e-02f, 1.00313257e-02f, -2.83245686e-02f, 4.47052345e-02f, -4.10458446e-02f, -4.50547226e-03f, -5.44734262e-02f, 2.62321010e-02f, 1.79862436e-02f, -1.23118766e-01f, -4.66951914e-02f, -1.35913221e-02f, 6.46710545e-02f, 3.57346772e-03f, -1.22234225e-02f, -1.79382376e-02f, -2.55502146e-02f, 2.37224065e-02f, 4.08669421e-03f, -6.51476011e-02f, 4.43651415e-02f, 4.68596332e-02f, -3.25175002e-02f, 4.02271142e-03f, -3.97607498e-03f, 1.11939451e-02f, -9.95597765e-02f, 3.33168246e-02f, 8.01060572e-02f, 9.42692459e-02f, -6.38294220e-02f, 3.23151797e-02f, -5.13553359e-02f, -7.49877188e-03f, 5.30047301e-34f, -4.13195118e-02f, 9.49647054e-02f, -1.06401421e-01f, 4.96590659e-02f, -3.41913216e-02f, -3.16745825e-02f, -1.71556100e-02f, 1.70102261e-03f, 5.79757839e-02f, -1.21776201e-03f, -1.68536007e-02f, -5.16912937e-02f, 5.52998893e-02f, -3.42647582e-02f, 3.08179390e-02f, -3.10481321e-02f, 9.27532911e-02f, 3.72663736e-02f, -2.37398390e-02f, 4.45893556e-02f, 1.46153290e-02f, 1.16239369e-01f, -5.00112809e-02f, 3.88716534e-02f, 4.24746517e-03f, 2.56976597e-02f, 3.27243991e-02f, 4.29907516e-02f, -1.36144664e-02f, 2.56122462e-02f, 1.06262704e-02f, -8.46863687e-02f, -9.52982306e-02f, 1.08399861e-01f, -7.51600116e-02f, -1.37773696e-02f, 6.37338236e-02f, -4.49668383e-03f, -3.25321481e-02f, 6.23613894e-02f, 3.48053388e-02f, -3.54922377e-02f, -2.00222749e-02f, 3.66608351e-02f, -2.48837117e-02f, 1.01818312e-02f, -7.01233074e-02f, -4.31950912e-02f, 2.95332875e-02f, -2.94925761e-04f, -3.45386788e-02f, 1.46676088e-02f, -9.83970016e-02f, -4.70488034e-02f, -8.85495264e-03f, -8.89913887e-02f, 3.50996181e-02f, -1.29601955e-01f, -4.98866327e-02f, -6.12047128e-02f, -5.97797595e-02f, 9.46318638e-03f, 4.91217636e-02f, -7.75026381e-02f, 8.09727386e-02f, -4.79257330e-02f, 2.34377384e-03f, 7.57031664e-02f, -2.40175538e-02f, -1.52545972e-02f, 4.86738645e-02f, -3.85968462e-02f, -7.04831555e-02f, -1.20348558e-02f, -3.88790444e-02f, -7.76017010e-02f, -1.07244095e-02f, 1.04188547e-02f, -2.13753711e-02f, -9.17386562e-02f, -1.11344922e-02f, -2.96066124e-02f, 2.46458314e-02f, 4.65713162e-03f, -1.63449813e-02f, -3.95219661e-02f, 7.73373842e-02f, -2.84732711e-02f, -3.69941373e-03f, 8.27665031e-02f, -1.10409120e-02f, 3.13983150e-02f, 5.35094403e-02f, 5.75145856e-02f, -3.17622274e-02f, -1.52911266e-08f, -7.99661428e-02f, -4.76797223e-02f, -8.59788507e-02f, 5.69616817e-02f, -4.08866219e-02f, 2.23832745e-02f, -4.64450521e-03f, -3.80130820e-02f, -3.10671162e-02f, -1.07277986e-02f, 1.97698399e-02f, 7.77001120e-03f, -6.09471835e-03f, -3.86376269e-02f, 2.80271862e-02f, 6.78137988e-02f, -2.35351231e-02f, 3.21747474e-02f, 8.02536216e-03f, -2.39107087e-02f, -1.21995783e-03f, 3.14598754e-02f, -5.24923652e-02f, -8.06815736e-03f, 3.14770546e-03f, 5.11496514e-02f, -4.44104522e-02f, 6.36013448e-02f, 3.85083966e-02f, 3.30433100e-02f, -4.18727705e-03f, 4.95592728e-02f, -5.69605269e-02f, -6.49712980e-03f, -2.49793101e-02f, -1.60867237e-02f, 6.62289783e-02f, -2.06310675e-02f, 1.08045749e-01f, 1.68547183e-02f, 1.43812457e-02f, -1.32127237e-02f, -1.29387408e-01f, 6.95216507e-02f, -5.55773005e-02f, -6.75413087e-02f, -5.45820361e-03f, -6.13595592e-03f, 3.90840955e-02f, -6.28779382e-02f, 3.74063551e-02f, -1.16570760e-02f, 1.29150180e-02f, -5.52495569e-02f, 5.16075864e-02f, -4.30842629e-03f, 5.80247641e-02f, 1.86945070e-02f, 2.27810256e-02f, 3.21665332e-02f, 5.37978970e-02f, 7.02848658e-02f, 7.49312267e-02f, -8.41774940e-02f])
        ];

        foreach (var (Input, Embedding) in samples)
        {
            IList<ReadOnlyMemory<float>> results = await service.GenerateEmbeddingsAsync([Input]);
            AssertEqualTolerance(Embedding, results[0].Span);
        }
    }

    [RetryFact(typeof(HttpRequestException))]
    public async Task ValidateSimilarityScoresOrderedForBgeMicroV2()
    {
        using BertOnnxTextEmbeddingGenerationService service = await GetBgeMicroV2ServiceAsync();

        string input = "What is an amphibian?";
        IList<ReadOnlyMemory<float>> inputResults = await service.GenerateEmbeddingsAsync([input]);

        string[] examples =
        [
            "A frog is an amphibian.",
            "It's not easy bein' green.",
            "A dog is a man's best friend.",
            "A tree is green.",
            "A dog is a mammal.",
            "Rachel, Monica, Phoebe, Joey, Chandler, Ross",
            "What is an amphibian?",
            "Frogs, toads, and salamanders are all examples.",
            "Cos'è un anfibio?",
            "You ain't never had a friend like me.",
            "Amphibians are four-limbed and ectothermic vertebrates of the class Amphibia.",
            "A frog is green.",
            "They are four-limbed and ectothermic vertebrates.",
        ];

        foreach (bool upper in new[] { false, true })
        {
            for (int trial = 0; trial < 3; trial++)
            {
                examples = [.. examples.OrderBy(e => Guid.NewGuid())]; // TODO: Random.Shared.Shuffle

                IList<ReadOnlyMemory<float>> examplesResults = await service.GenerateEmbeddingsAsync(
                    examples.Select(s => upper ? s.ToUpperInvariant() : s).ToList());

                string[] sortedExamples = examples
                    .Zip(examplesResults)
                    .OrderByDescending(p => TensorPrimitives.CosineSimilarity(inputResults[0].Span, p.Second.Span))
                    .Select(p => p.First)
                    .ToArray();

                Assert.Equal(
                    new string[]
                    {
                        "What is an amphibian?",
                        "A frog is an amphibian.",
                        "Amphibians are four-limbed and ectothermic vertebrates of the class Amphibia.",
                        "Frogs, toads, and salamanders are all examples.",
                        "A frog is green.",
                        "Cos'è un anfibio?",
                        "They are four-limbed and ectothermic vertebrates.",
                        "A dog is a mammal.",
                        "A tree is green.",
                        "It's not easy bein' green.",
                        "A dog is a man's best friend.",
                        "You ain't never had a friend like me.",
                        "Rachel, Monica, Phoebe, Joey, Chandler, Ross",
                    },
                    sortedExamples);
            }
        }
    }

    [RetryFact(typeof(HttpRequestException))]
    public async Task ValidateServiceMayBeUsedConcurrently()
    {
        using BertOnnxTextEmbeddingGenerationService service = await GetBgeMicroV2ServiceAsync();

        string input = "What is an amphibian?";
        IList<ReadOnlyMemory<float>> inputResults = await service.GenerateEmbeddingsAsync([input]);

        string[] examples =
        [
            "A frog is an amphibian.",
            "It's not easy bein' green.",
            "A dog is a man's best friend.",
            "A tree is green.",
            "A dog is a mammal.",
            "Rachel, Monica, Phoebe, Joey, Chandler, Ross",
            "What is an amphibian?",
            "Frogs, toads, and salamanders are all examples.",
            "Cos'è un anfibio?",
            "You ain't never had a friend like me.",
            "Amphibians are four-limbed and ectothermic vertebrates of the class Amphibia.",
            "A frog is green.",
            "They are four-limbed and ectothermic vertebrates.",
        ];

        for (int trial = 0; trial < 10; trial++)
        {
            IList<ReadOnlyMemory<float>> examplesResults =
                (await Task.WhenAll(examples.Select(e => service.GenerateEmbeddingsAsync([e])))).SelectMany(e => e).ToList();

            string[] sortedExamples = examples
                .Zip(examplesResults)
                .OrderByDescending(p => TensorPrimitives.CosineSimilarity(inputResults[0].Span, p.Second.Span))
                .Select(p => p.First)
                .ToArray();

            Assert.Equal(
                new string[]
                {
                    "What is an amphibian?",
                    "A frog is an amphibian.",
                    "Amphibians are four-limbed and ectothermic vertebrates of the class Amphibia.",
                    "Frogs, toads, and salamanders are all examples.",
                    "A frog is green.",
                    "Cos'è un anfibio?",
                    "They are four-limbed and ectothermic vertebrates.",
                    "A dog is a mammal.",
                    "A tree is green.",
                    "It's not easy bein' green.",
                    "A dog is a man's best friend.",
                    "You ain't never had a friend like me.",
                    "Rachel, Monica, Phoebe, Joey, Chandler, Ross",
                },
                sortedExamples);
        }
    }

    private static void AssertEqualTolerance(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        Assert.Equal(left.Length, right.Length);

        for (int i = 0; i < left.Length; i++)
        {
            Assert.True(IsEqualWithTolerance(left[i], right[i]), $"{left[i]} != {right[i]} at [{i}]");
        }
    }

    private static bool IsEqualWithTolerance(float expected, float actual)
    {
        const float Tolerance = 0.0000008f;
        float diff = MathF.Abs(expected - actual);
        return
            diff <= Tolerance ||
            diff <= MathF.Max(MathF.Abs(expected), MathF.Abs(actual)) * Tolerance;
    }

    private static async Task<string> GetTestFilePath(string url)
    {
        // Rather than downloading each model on each use, try to cache it into a temporary file.
        // The file's name is computed as a hash of the url.

        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".cachedtestfile";
        string path = Path.Join(Path.GetTempPath(), name);

        if (!File.Exists(path))
        {
            using Stream responseStream = await s_client.GetStreamAsync(url);
            try
            {
                using FileStream dest = File.OpenWrite(path);
                await responseStream.CopyToAsync(dest);
            }
            catch
            {
                try { File.Delete(path); } catch { } // if something goes wrong, try not to leave a bad file in place
                throw;
            }
        }

        return path;
    }

    private const string BgeMicroV2ModelUrl = "https://huggingface.co/TaylorAI/bge-micro-v2/resolve/f09f671/onnx/model.onnx";
    private const string BgeMicroV2VocabUrl = "https://huggingface.co/TaylorAI/bge-micro-v2/raw/f09f671/vocab.txt";

    private static async Task<BertOnnxTextEmbeddingGenerationService> GetBgeMicroV2ServiceAsync() =>
        await BertOnnxTextEmbeddingGenerationService.CreateAsync(
            await GetTestFilePath(BgeMicroV2ModelUrl),
            await GetTestFilePath(BgeMicroV2VocabUrl));

    private static async Task<BertOnnxTextEmbeddingGenerationService> GetAllMiniLML6V2Async() =>
        await BertOnnxTextEmbeddingGenerationService.CreateAsync(
            await GetTestFilePath("https://huggingface.co/optimum/all-MiniLM-L6-v2/resolve/1024484/model.onnx"),
            await GetTestFilePath("https://huggingface.co/optimum/all-MiniLM-L6-v2/raw/1024484/vocab.txt"),
            new BertOnnxOptions { NormalizeEmbeddings = true });
}
