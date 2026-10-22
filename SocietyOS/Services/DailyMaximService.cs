using System;
using System.Collections.Generic;
using SocietyOS.Models;

namespace SocietyOS.Services
{
    public interface IDailyMaximService
    {
        SuvicharItem GetTodaysMaxim();
    }

    public class DailyMaximService : IDailyMaximService
    {
        private static readonly List<SuvicharItem> CuratedMaxims = new()
        {
            new SuvicharItem
            {
                HindiQuote = "विद्या ददाति विनयं, विनयाद्याति पात्रताम्।",
                Transliteration = "Vidya Dadati Vinayam, Vinayad Yati Patratam",
                EnglishMeaning = "Knowledge bestows humility; humility leads to worthiness.",
                Source = "हितोपदेश (Hitopadesha)"
            },
            new SuvicharItem
            {
                HindiQuote = "वसुधैव कुटुम्बकम्।",
                Transliteration = "Vasudhaiva Kutumbakam",
                EnglishMeaning = "The whole world is one single community.",
                Source = "महोपनिषद् (Maha Upanishad)"
            },
            new SuvicharItem
            {
                HindiQuote = "सत्यमेव जयते नानृतम्।",
                Transliteration = "Satyameva Jayate Nanritam",
                EnglishMeaning = "Truth alone triumphs, not falsehood.",
                Source = "मुण्डकोपनिषद् (Mundaka Upanishad)"
            },
            new SuvicharItem
            {
                HindiQuote = "कर्मण्येवाधिकारस्ते मा फलेषु कदाचन।",
                Transliteration = "Karmanye Vadhikaraste Ma Phaleshu Kadachana",
                EnglishMeaning = "Perform your duty with dedication, free from fixation on results.",
                Source = "श्रीमद्भगवद्गीता (Bhagavad Gita)"
            },
            new SuvicharItem
            {
                HindiQuote = "उद्यमेन हि सिध्यन्ति कार्याणि न मनोरथैः।",
                Transliteration = "Udyamena Hi Sidhyanti Karyani Na Manorathaih",
                EnglishMeaning = "Goals are accomplished through diligent action, not daydreaming.",
                Source = "हितोपदेश (Hitopadesha)"
            },
            new SuvicharItem
            {
                HindiQuote = "परोपकाराय फलन्ति वृक्षाः परोपकाराय वहन्ति नद्यः।",
                Transliteration = "Paropakaraya Phalanti Vrikshah, Paropakaraya Vahanti Nadyah",
                EnglishMeaning = "Trees bear fruit to nourish others; rivers flow to give life to all.",
                Source = "सुभाषित (Subhashita)"
            },
            new SuvicharItem
            {
                HindiQuote = "संहतिः श्रेयसी पुंसां स्वकुलैः सह बान्धवैः।",
                Transliteration = "Samhatih Shreyasi Pumsam",
                EnglishMeaning = "Unity and harmony among neighbours bring collective prosperity.",
                Source = "पञ्चतन्त्र (Panchatantra)"
            }
        };

        public SuvicharItem GetTodaysMaxim()
        {
            var dayOfYear = DateTime.Today.DayOfYear;
            var index = (dayOfYear - 1) % CuratedMaxims.Count;
            var selected = CuratedMaxims[index];
            selected.DayNumber = dayOfYear;
            return selected;
        }
    }
}