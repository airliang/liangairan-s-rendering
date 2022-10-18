#pragma once
#include <string>
#include <sstream>

enum class Color { Red, Green, Blue, Black, White, Yellow, Orange };
typedef void(*FuncCallBack)(const char* message, int color, int size);

class Logger
{
public:
    static void Log(const char* message, Color color = Color::Green);
    static void Log(const std::string message, Color color = Color::Green);

    
    static FuncCallBack logCallbackFunc;
private:
    static void SendLog(const std::stringstream& ss, const Color& color);
};
