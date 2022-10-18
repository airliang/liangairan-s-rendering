#include "log.h"
#include "../../BvhBuilder.h"

FuncCallBack Logger::logCallbackFunc = nullptr;

void Logger::Log(const char* message, Color color) 
{
    if (logCallbackFunc != nullptr)
        logCallbackFunc(message, (int)color, (int)strlen(message));
}

void  Logger::Log(const std::string message, Color color) {
    const char* tmsg = message.c_str();
    if (logCallbackFunc != nullptr)
        logCallbackFunc(tmsg, (int)color, (int)strlen(tmsg));
}

void Logger::SendLog(const std::stringstream& ss, const Color& color) {
    const std::string tmp = ss.str();
    const char* tmsg = tmp.c_str();
    if (logCallbackFunc != nullptr)
        logCallbackFunc(tmsg, (int)color, (int)strlen(tmsg));
}
