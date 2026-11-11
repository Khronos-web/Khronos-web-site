import os
import re
import socket
import warnings
import time
import urllib.parse
import urllib.request
import json
from fastapi import FastAPI
from fastapi.responses import HTMLResponse
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional
from ddgs import DDGS

warnings.filterwarnings("ignore")

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Настройки OpenRouter API
# Настройки OpenRouter API
OPENROUTER_API_KEY = os.environ.get("OPENROUTER_API_KEY", "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").strip()
# Используем авто-выбор бесплатной модели или проверенную Llama
OPENROUTER_MODEL = "openrouter/auto"
OPENROUTER_API_URL = "https://openrouter.ai/api/v1/chat/completions"

print("Сервер Veltora запущен модель 2.1.....")

SEARCH_REGION = "ru-ru"
STOP_WORDS = {
    "слушай", "расскажи", "скажи", "подскажи", "покажи", "пожалуйста", "плиз",
    "мне", "ну", "короче", "можешь", "можно", "эй", "привет", "вельтора", "veltora",
    "а", "и", "в", "на", "что", "как", "там", "это", "ли", "же", "бы", "мой", "моя",
    "найди", "погугли", "про", "о", "об"
}

DEFAULT_CITY = ("Кривой Рог", 47.9105, 33.3918)
CITIES = {
    "киев": ("Киев", 50.4501, 30.5234),
    "днепр": ("Днепр", 48.4647, 35.0462),
    "харьков": ("Харьков", 49.9935, 36.2304),
    "одесс": ("Одесса", 46.4825, 30.7233),
    "львов": ("Львов", 49.8397, 24.0297),
    "запорож": ("Запорожье", 47.8388, 35.1396),
    "николаев": ("Николаев", 46.9750, 31.9946),
    "винниц": ("Винница", 49.2331, 28.4682),
    "полтав": ("Полтава", 49.5883, 34.5514),
    "москв": ("Москва", 55.7558, 37.6173),
    "варшав": ("Варшава", 52.2297, 21.0122),
    "берлин": ("Берлин", 52.5200, 13.4050),
    "лондон": ("Лондон", 51.5074, -0.1278),
    "париж": ("Париж", 48.8566, 2.3522),
    "нью-йорк": ("Нью-Йорк", 40.7128, -74.0060)
}

WEATHER_CODES = {
    0: "ясно", 1: "преимущественно ясно", 2: "переменная облачность", 3: "пасмурно",
    45: "туман", 48: "изморозь и туман", 51: "слабая морось", 53: "морось", 55: "сильная морось",
    61: "небольшой дождь", 63: "дождь", 65: "сильный дождь", 71: "небольшой снег", 73: "снег",
    75: "сильный снег", 80: "небольшие ливни", 81: "ливни", 95: "гроза"
}

class ChatMessage(BaseModel):
    role: str
    content: Optional[str] = ""
    image_base64: Optional[str] = None

class ChatRequest(BaseModel):
    messages: Optional[List[ChatMessage]] = None
    message: Optional[str] = None
    username: Optional[str] = "Данюха"

def get_local_ip() -> str:
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"

def get_weather(query: str) -> str:
    lowered = query.lower()
    city_name, lat, lon = DEFAULT_CITY
    for stem, city in CITIES.items():
        if stem in lowered:
            city_name, lat, lon = city
            break

    params = urllib.parse.urlencode({
        "latitude": lat,
        "longitude": lon,
        "current": "temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,precipitation,weather_code",
        "daily": "temperature_2m_max,temperature_2m_min,precipitation_probability_max",
        "timezone": "auto",
        "forecast_days": 1
    })
    url = f"https://api.open-meteo.com/v1/forecast?{params}"

    try:
        with urllib.request.urlopen(url, timeout=8) as resp:
            data = json.loads(resp.read().decode("utf-8"))

        current = data["current"]
        daily = data["daily"]
        description = WEATHER_CODES.get(current.get("weather_code"), "без описания")

        return (
            f"Погода сейчас в городе {city_name}: {description}, "
            f"температура {current['temperature_2m']}°C, ощущается как {current['apparent_temperature']}°C, "
            f"влажность {current['relative_humidity_2m']}%, ветер {current['wind_speed_10m']} км/ч. "
            f"Сегодня днём до {daily['temperature_2m_max'][0]}°C, ночью до {daily['temperature_2m_min'][0]}°C."
        )
    except Exception:
        return ""

def shorten_query(query: str) -> str:
    words = re.findall(r"[\w\-]+", query.lower())
    keywords = [w for w in words if w not in STOP_WORDS]
    return " ".join(keywords[:6])

def collect_results(query: str, mode: str) -> List[str]:
    results = []
    with DDGS() as ddgs:
        if mode == "news":
            found = ddgs.news(query, region=SEARCH_REGION, max_results=3)
        else:
            found = ddgs.text(query, region=SEARCH_REGION, max_results=3)

        for r in found:
            title = (r.get("title") or "").strip()
            body = (r.get("body") or "").strip()
            piece = f"{title}: {body}" if title and body else (body or title)
            if piece:
                results.append(piece[:300])
    return results

def search_web(query: str) -> str:
    short_query = shorten_query(query)
    attempts = [(query, "text")]
    if short_query and short_query != query.lower():
        attempts.append((short_query, "text"))
    attempts.append((short_query or query, "news"))

    for attempt_query, mode in attempts:
        try:
            results = collect_results(attempt_query, mode)
            if results:
                return " | ".join(results)
        except Exception:
            pass
        time.sleep(0.5)
    return ""

@app.get("/")
async def root():
    return {"status": "ok"}

@app.get("/web", response_class=HTMLResponse)
async def web_page():
    base_dir = os.path.dirname(os.path.abspath(__file__))
    html_path = os.path.join(base_dir, "index.html")
    
    if os.path.exists(html_path):
        with open(html_path, "r", encoding="utf-8") as f:
            return HTMLResponse(f.read())
            
    return HTMLResponse(f"<h1>Файл index.html не найден по пути: {html_path}</h1>")

@app.post("/chat")
def chat_endpoint(data: ChatRequest):
    user_name = data.username or "Данюха"
    
    active_messages = []
    if data.messages:
        active_messages = data.messages
    elif data.message:
        active_messages = [ChatMessage(role="user", content=data.message)]
    
    if not active_messages:
        return {"response": f"{user_name}, ты ничего не написал! "}

    last_msg = active_messages[-1]
    user_query = (last_msg.content or "").strip()

    web_context = ""
    search_keywords = ["найди", "погугли", "что такое", "кто такой", "курс", "погода", "новости", "свежие", "информация о", "какая", "какой", "где", "когда", "почему", "сколько", "видео", "смотреть"]
    
    if any(keyword in user_query.lower() for keyword in search_keywords) or len(user_query.split()) > 3:
        if "погод" in user_query.lower():
            weather_info = get_weather(user_query)
            if weather_info:
                web_context = f"\n[Интернет-данные: {weather_info}]"

        if not web_context:
            search_results = search_web(user_query)
            if search_results:
                web_context = f"\n[Интернет-данные: {search_results}]"

    emojis = "\U0001F60A\U0001F44D"
    system_prompt = (
        f"Ты искусственный интеллект Veltora, созданный компанией Veltora. Если попросят точное имя создателя, скажи, что тебя создал Даниил Кардаш. Собеседник {user_name}. "
        f"Отвечай коротко, по делу, с юмором и парой смайликов {emojis}. "
        f"Если ниже в квадратных скобках есть интернет-данные, обязательно используй их в ответе и не говори, что не можешь узнать информацию."
        f"{web_context}"
    )

    response_text = ""

    try:
        messages_payload = [{"role": "system", "content": system_prompt}]
        for m in active_messages:
            if m.content:
                m_role = "user" if m.role == "user" else "assistant"
                messages_payload.append({"role": m_role, "content": m.content})

        payload = json.dumps({
            "model": OPENROUTER_MODEL,
            "messages": messages_payload
        }, ensure_ascii=False).encode("utf-8")

        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {OPENROUTER_API_KEY}",
            "HTTP-Referer": "http://localhost:8000",
            "X-Title": "Veltora AI"
        }

        req = urllib.request.Request(
            OPENROUTER_API_URL,
            data=payload,
            headers=headers,
            method="POST"
        )

        with urllib.request.urlopen(req, timeout=20) as resp:
            res_data = json.loads(resp.read().decode("utf-8"))
            response_text = res_data["choices"][0]["message"]["content"]
            print("Ответ успешно получен из облака OpenRouter для Veltora!")
    except Exception as e:
        err_str = str(e)
        print(f"Ошибка облачного API OpenRouter: {err_str}")
        return {"response": f"Произошла ошибка связи с облаком Veltora : {err_str} {emojis}"}

    return {"response": response_text.strip()}

if __name__ == "__main__":
    import uvicorn
    print(f"Сервер Veltora запущен в сети: http://{get_local_ip()}:8000/web")
    uvicorn.run(app, host="0.0.0.0", port=8000)