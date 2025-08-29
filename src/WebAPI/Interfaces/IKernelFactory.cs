using Microsoft.SemanticKernel;

namespace WebAPI.Interfaces;

public interface IKernelFactory
{
    Kernel CreateKernel();
}